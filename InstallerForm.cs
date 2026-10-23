using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace YunseulMemo;

/// <summary>
/// YUNSEUL-EDITOR 설치 마법사 폼 (YUNSEUL-2ISHARE 스타일).
/// YUNSEUL-EDITOR-Setup.exe 실행 시 자동으로 표시됩니다.
/// </summary>
public sealed class InstallerForm : Form
{
    private const string AppName = "YUNSEUL-EDITOR";
    private const string ExeName = "YUNSEUL-EDITOR.exe";
    private const string AppDescription = "가볍고 빠른 한국형 고성능 텍스트/문서 에디터";

    private static readonly Color ColorBg = Color.FromArgb(15, 23, 42);        // #0f172a
    private static readonly Color ColorCard = Color.FromArgb(30, 41, 59);      // #1e293b
    private static readonly Color ColorLine = Color.FromArgb(51, 65, 85);      // #334155
    private static readonly Color ColorText = Color.FromArgb(248, 250, 252);   // #f8fafc
    private static readonly Color ColorMuted = Color.FromArgb(148, 163, 184);  // #94a3b8
    private static readonly Color ColorAccent = Color.FromArgb(56, 189, 248);  // #38bdf8
    private static readonly Color ColorBtn = Color.FromArgb(37, 99, 235);      // #2563eb
    private static readonly Color ColorBtnHover = Color.FromArgb(29, 78, 216); // #1d4ed8

    private readonly string _defaultInstallDir;
    private int _currentPage = 0;
    private string _installedExePath = "";

    // Controls
    private readonly Panel _bodyPanel;
    private readonly Panel[] _pages = new Panel[4];
    private readonly Button _btnBack;
    private readonly Button _btnNext;
    private readonly Button _btnCancel;

    // Page 1 Controls (Directory & Options)
    private TextBox _txtInstallDir = null!;
    private CheckBox _chkDesktop = null!;
    private CheckBox _chkStartMenu = null!;

    // Page 2 Controls (Progress)
    private ProgressBar _progressBar = null!;
    private Label _lblProgressStatus = null!;

    // Page 3 Controls (Finish)
    private CheckBox _chkRunApp = null!;

    public InstallerForm()
    {
        // 1. 자기 자신에 대한 스마트 스크린/Zone.Identifier 차단 해제
        UnblockPath(Environment.ProcessPath ?? "");

        _defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);

        Text = $"{AppName} Setup";
        ClientSize = new Size(620, 520);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        BackColor = ColorBg;
        ForeColor = ColorText;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // 아이콘 적용
        try
        {
            if (File.Exists("app_icon.ico"))
            {
                Icon = new Icon("app_icon.ico");
            }
            else if (Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") is { } appIco)
            {
                Icon = appIco;
            }
        }
        catch { }

        // 하단 버튼 바
        var bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = ColorBg,
            Padding = new Padding(24, 10, 24, 15)
        };

        _btnCancel = CreateButton("취소", (s, e) => Close(), isSecondary: true);
        _btnCancel.Dock = DockStyle.Right;

        var spacing1 = new Panel { Dock = DockStyle.Right, Width = 10, BackColor = ColorBg };

        _btnNext = CreateButton("다음 >", OnNextClick, isSecondary: false);
        _btnNext.Dock = DockStyle.Right;

        var spacing2 = new Panel { Dock = DockStyle.Right, Width = 10, BackColor = ColorBg };

        _btnBack = CreateButton("< 이전", OnBackClick, isSecondary: true);
        _btnBack.Dock = DockStyle.Right;
        _btnBack.Visible = false;

        bottomBar.Controls.Add(_btnBack);
        bottomBar.Controls.Add(spacing2);
        bottomBar.Controls.Add(_btnNext);
        bottomBar.Controls.Add(spacing1);
        bottomBar.Controls.Add(_btnCancel);

        // 메인 본문 패널
        _bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorBg,
            Padding = new Padding(24, 20, 24, 10)
        };

        Controls.Add(_bodyPanel);
        Controls.Add(bottomBar);

        // 페이지들 생성
        _pages[0] = BuildIntroPage();
        _pages[1] = BuildDirPage();
        _pages[2] = BuildProgressPage();
        _pages[3] = BuildFinishPage();

        foreach (var page in _pages)
        {
            page.Dock = DockStyle.Fill;
            _bodyPanel.Controls.Add(page);
        }

        ShowPage(0);
    }

    private Button CreateButton(string text, EventHandler onClick, bool isSecondary = false)
    {
        var btn = new Button
        {
            Text = text,
            Width = 105,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ColorText,
            BackColor = isSecondary ? ColorCard : ColorBtn,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = isSecondary ? 1 : 0;
        btn.FlatAppearance.BorderColor = ColorLine;
        btn.FlatAppearance.MouseOverBackColor = isSecondary ? ColorLine : ColorBtnHover;
        btn.Click += onClick;
        return btn;
    }

    private Panel BuildIntroPage()
    {
        var panel = new Panel { BackColor = ColorBg };

        var title = new Label
        {
            Text = $"{AppName} 설치 마법사",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = ColorAccent,
            AutoSize = true,
            Location = new Point(10, 15)
        };

        var subtitle = new Label
        {
            Text = "한국어 문서 작성과 코드 편집을 위한 스마트 에디터에 오신 것을 환영합니다.",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(12, 54)
        };

        var card = new Panel
        {
            BackColor = ColorCard,
            Location = new Point(10, 100),
            Size = new Size(550, 270),
            Padding = new Padding(20)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorLine, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var infoTitle = new Label
        {
            Text = "주요 특징 및 기능 안내",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ColorText,
            AutoSize = true,
            Location = new Point(15, 15)
        };

        var features = new[]
        {
            "• 가볍고 쾌속한 다중 문서 탭 (MDI/Tabbed) 편집 환경",
            "• 정밀 눈금자 및 라인 번호, 편리한 북마크/URL 자동 하이퍼링크",
            "• EditPlus 감성의 익숙하고 직관적인 도구 모음 및 단축키",
            "• 대용량 텍스트 고속 뷰어 및 완벽한 한국어(CP949/UTF-8) 인코딩 지원",
            "• 관리자 권한 없이도 사용자 계정에 안전하고 깔끔하게 설치"
        };

        int curY = 55;
        foreach (var feat in features)
        {
            var lbl = new Label
            {
                Text = feat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = ColorMuted,
                AutoSize = true,
                Location = new Point(18, curY)
            };
            card.Controls.Add(lbl);
            curY += 32;
        }

        var hint = new Label
        {
            Text = "계속 진행하려면 [다음] 버튼을 클릭하세요.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(10, 390)
        };

        card.Controls.Add(infoTitle);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(card);
        panel.Controls.Add(hint);

        return panel;
    }

    private Panel BuildDirPage()
    {
        var panel = new Panel { BackColor = ColorBg };

        var title = new Label
        {
            Text = "설치 위치 및 바로가기 옵션",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = ColorAccent,
            AutoSize = true,
            Location = new Point(10, 15)
        };

        var subtitle = new Label
        {
            Text = "프로그램이 설치될 경로와 생성할 바로가기를 선택하세요.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(12, 54)
        };

        var card = new Panel
        {
            BackColor = ColorCard,
            Location = new Point(10, 100),
            Size = new Size(550, 160),
            Padding = new Padding(20)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorLine, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var dirLabel = new Label
        {
            Text = "설치 폴더:",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ColorText,
            AutoSize = true,
            Location = new Point(15, 18)
        };

        _txtInstallDir = new TextBox
        {
            Text = _defaultInstallDir,
            Font = new Font("Segoe UI", 10f),
            ForeColor = ColorText,
            BackColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(18, 48),
            Size = new Size(410, 30)
        };

        var btnBrowse = CreateButton("찾아보기...", (s, e) =>
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "설치할 폴더를 선택하세요.";
            dlg.SelectedPath = _txtInstallDir.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtInstallDir.Text = Path.Combine(dlg.SelectedPath, AppName);
            }
        }, isSecondary: true);
        btnBrowse.Size = new Size(95, 30);
        btnBrowse.Location = new Point(436, 47);

        var spaceLabel = new Label
        {
            Text = "필요 디스크 공간: 약 5MB 미만",
            Font = new Font("Segoe UI", 9f),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(18, 92)
        };

        card.Controls.Add(dirLabel);
        card.Controls.Add(_txtInstallDir);
        card.Controls.Add(btnBrowse);
        card.Controls.Add(spaceLabel);

        var optCard = new Panel
        {
            BackColor = ColorCard,
            Location = new Point(10, 275),
            Size = new Size(550, 105),
            Padding = new Padding(20)
        };
        optCard.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorLine, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, optCard.Width - 1, optCard.Height - 1);
        };

        _chkDesktop = new CheckBox
        {
            Text = "바탕화면에 바로가기 아이콘 생성",
            Checked = true,
            ForeColor = ColorText,
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(18, 18)
        };

        _chkStartMenu = new CheckBox
        {
            Text = "시작 메뉴 프로그램 목록에 추가",
            Checked = true,
            ForeColor = ColorText,
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(18, 55)
        };

        optCard.Controls.Add(_chkDesktop);
        optCard.Controls.Add(_chkStartMenu);

        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(card);
        panel.Controls.Add(optCard);

        return panel;
    }

    private Panel BuildProgressPage()
    {
        var panel = new Panel { BackColor = ColorBg };

        var title = new Label
        {
            Text = "설치 진행 중",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = ColorAccent,
            AutoSize = true,
            Location = new Point(10, 15)
        };

        var subtitle = new Label
        {
            Text = "파일을 복사하고 시스템 환경을 구성하고 있습니다...",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(12, 54)
        };

        var card = new Panel
        {
            BackColor = ColorCard,
            Location = new Point(10, 110),
            Size = new Size(550, 180),
            Padding = new Padding(25)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorLine, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(25, 45),
            Size = new Size(500, 24),
            Style = ProgressBarStyle.Continuous,
            Value = 0
        };

        _lblProgressStatus = new Label
        {
            Text = "설치 준비 중...",
            Font = new Font("Segoe UI", 10f),
            ForeColor = ColorAccent,
            Location = new Point(25, 90),
            Size = new Size(500, 45)
        };

        card.Controls.Add(_progressBar);
        card.Controls.Add(_lblProgressStatus);

        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(card);

        return panel;
    }

    private Panel BuildFinishPage()
    {
        var panel = new Panel { BackColor = ColorBg };

        var title = new Label
        {
            Text = "설치 완료!",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = ColorAccent,
            AutoSize = true,
            Location = new Point(10, 15)
        };

        var subtitle = new Label
        {
            Text = $"{AppName} 설치가 성공적으로 완료되었습니다.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ColorMuted,
            AutoSize = true,
            Location = new Point(12, 54)
        };

        var card = new Panel
        {
            BackColor = ColorCard,
            Location = new Point(10, 100),
            Size = new Size(550, 240),
            Padding = new Padding(25)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorLine, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var completeMsg = new Label
        {
            Text = "지금 바로 윤슬 에디터를 실행하여 문서 및 코드를 편집할 수 있습니다.\n설치 폴더와 바로가기가 정상적으로 등록되었습니다.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ColorText,
            Location = new Point(20, 25),
            Size = new Size(510, 60)
        };

        _chkRunApp = new CheckBox
        {
            Text = $"{AppName} 지금 바로 실행",
            Checked = true,
            ForeColor = ColorAccent,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 110)
        };

        card.Controls.Add(completeMsg);
        card.Controls.Add(_chkRunApp);

        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(card);

        return panel;
    }

    private void ShowPage(int index)
    {
        _currentPage = index;
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visible = (i == index);
        }

        switch (index)
        {
            case 0:
                _btnBack.Visible = false;
                _btnNext.Visible = true;
                _btnNext.Text = "다음 >";
                _btnCancel.Visible = true;
                break;
            case 1:
                _btnBack.Visible = true;
                _btnNext.Visible = true;
                _btnNext.Text = "설치 시작";
                _btnCancel.Visible = true;
                break;
            case 2:
                _btnBack.Visible = false;
                _btnNext.Visible = false;
                _btnCancel.Visible = false;
                break;
            case 3:
                _btnBack.Visible = false;
                _btnNext.Visible = true;
                _btnNext.Text = "마침";
                _btnCancel.Visible = false;
                break;
        }
    }

    private void OnNextClick(object? sender, EventArgs e)
    {
        if (_currentPage == 0)
        {
            ShowPage(1);
        }
        else if (_currentPage == 1)
        {
            ShowPage(2);
            StartInstallation();
        }
        else if (_currentPage == 3)
        {
            if (_chkRunApp.Checked && File.Exists(_installedExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _installedExePath,
                        WorkingDirectory = Path.GetDirectoryName(_installedExePath) ?? "",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
            Close();
        }
    }

    private void OnBackClick(object? sender, EventArgs e)
    {
        if (_currentPage == 1)
        {
            ShowPage(0);
        }
    }

    private async void StartInstallation()
    {
        string targetDir = _txtInstallDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            targetDir = _defaultInstallDir;
        }

        bool createDesktop = _chkDesktop.Checked;
        bool createStartMenu = _chkStartMenu.Checked;

        try
        {
            await Task.Run(() => PerformInstall(targetDir, createDesktop, createStartMenu));
            ShowPage(3);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"설치 중 오류가 발생했습니다:\n{ex.Message}", "설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowPage(1);
        }
    }

    private void UpdateProgress(int percent, string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateProgress(percent, message));
            return;
        }
        _progressBar.Value = Math.Clamp(percent, 0, 100);
        _lblProgressStatus.Text = message;
    }

    private void PerformInstall(string targetDir, bool createDesktop, bool createStartMenu)
    {
        UpdateProgress(5, "실행 중인 인스턴스 확인 및 정리...");

        // 실행 중인 이전 버전 종료
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /IM {ExeName}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(1000);
        }
        catch { }

        Thread.Sleep(300);

        UpdateProgress(20, "설치 디렉토리 준비 중...");
        Directory.CreateDirectory(targetDir);

        UpdateProgress(40, "실행 바이너리 복사 및 설치 중...");
        string currentExe = Environment.ProcessPath ?? "";
        _installedExePath = Path.Combine(targetDir, ExeName);

        // Zone.Identifier 메타데이터 상속 방지를 위해 순수 바이너리 바이트 스트림으로 생성
        if (File.Exists(currentExe))
        {
            using (var src = File.OpenRead(currentExe))
            using (var dst = File.Create(_installedExePath))
            {
                src.CopyTo(dst);
            }
        }
        else
        {
            throw new FileNotFoundException("설치 원본 바이너리를 찾을 수 없습니다.", currentExe);
        }

        // 아이콘 복사 (존재 시)
        string iconSrc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
        string iconDst = Path.Combine(targetDir, "app_icon.ico");
        if (File.Exists(iconSrc))
        {
            try { File.Copy(iconSrc, iconDst, overwrite: true); } catch { }
        }

        UpdateProgress(60, "Windows 보안 차단 해제 적용 중...");
        UnblockPath(_installedExePath);
        if (File.Exists(iconDst)) UnblockPath(iconDst);

        UpdateProgress(75, "바로가기 생성 중...");
        string iconTarget = File.Exists(iconDst) ? iconDst : _installedExePath;

        if (createDesktop)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktop))
            {
                string lnk = Path.Combine(desktop, $"{AppName}.lnk");
                CreateShortcut(_installedExePath, lnk, iconTarget, AppDescription);
            }
        }

        if (createStartMenu)
        {
            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs",
                AppName);
            Directory.CreateDirectory(startMenu);
            string lnk = Path.Combine(startMenu, $"{AppName}.lnk");
            CreateShortcut(_installedExePath, lnk, iconTarget, AppDescription);
        }

        UpdateProgress(95, "최종 환경 설정 적용 중...");
        UnblockPath(targetDir);

        Thread.Sleep(400);
        UpdateProgress(100, "설치가 완료되었습니다.");
    }

    private static void UnblockPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path))) return;

        // 1. 파일인 경우 NTFS 대체 데이터 스트림(:Zone.Identifier) 제거
        if (File.Exists(path))
        {
            try
            {
                string zone = path + ":Zone.Identifier";
                if (File.Exists(zone)) File.Delete(zone);
            }
            catch { }
        }

        // 2. PowerShell Unblock-File 실행
        try
        {
            string psCmd = Directory.Exists(path)
                ? $"Get-ChildItem -LiteralPath '{path}' -Recurse -Force -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue; Unblock-File -LiteralPath '{path}' -ErrorAction SilentlyContinue"
                : $"Unblock-File -LiteralPath '{path}' -ErrorAction SilentlyContinue";

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -Command \"{psCmd}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(1500);
        }
        catch { }
    }

    private static void CreateShortcut(string targetPath, string shortcutPath, string iconPath, string description)
    {
        try
        {
            string workDir = Path.GetDirectoryName(targetPath) ?? "";
            string psCmd =
                $"$ws = New-Object -ComObject WScript.Shell; " +
                $"$s = $ws.CreateShortcut('{shortcutPath}'); " +
                $"$s.TargetPath = '{targetPath}'; " +
                $"$s.WorkingDirectory = '{workDir}'; " +
                (string.IsNullOrEmpty(iconPath) ? "" : $"$s.IconLocation = '{iconPath}'; ") +
                (string.IsNullOrEmpty(description) ? "" : $"$s.Description = '{description}'; ") +
                $"$s.Save();";

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -Command \"{psCmd}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(2000);
            UnblockPath(shortcutPath);
        }
        catch { }
    }
}
