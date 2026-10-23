using System.Text;

namespace YunseulMemo;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // .NET(Core) 이후에는 CP949(한국어 ANSI) 같은 코드 페이지가 기본 포함되어 있지 않으므로 등록이 필요합니다.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();

        // Setup 실행 모드 감지 (YUNSEUL-2ISHARE 스타일)
        if (IsSetupMode(args))
        {
            Application.Run(new InstallerForm());
            return;
        }

        // 명령줄 인자(또는 "연결 프로그램"/드래그로 전달된 파일)를 바로 엽니다.
        Application.Run(new MainForm(args));
    }

    private static bool IsSetupMode(string[] args)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, "--app", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--setup", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        string exePath = Environment.ProcessPath ?? "";
        string fileName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
        return fileName.Contains("setup");
    }
}
