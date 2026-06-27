using System.Runtime.InteropServices;

namespace YunseulMemo;

/// <summary>
/// 이미지+텍스트 혼합 붙여넣기를 지원하는 RichTextBox.
/// 기본 RichTextBox의 Ctrl+V는 이미지가 포함된 클립보드 데이터에서 텍스트만 붙여넣는 문제가 있어,
/// WM_PASTE를 가로채서 RTF/이미지를 올바르게 처리합니다.
/// </summary>
public class ResizableRichTextBox : RichTextBox
{
    private const int WM_PASTE = 0x0302;

    /// <summary>
    /// 외부에서 커스텀 붙여넣기 로직을 주입할 수 있는 이벤트.
    /// 핸들러가 true를 반환하면 기본 WM_PASTE를 차단합니다.
    /// </summary>
    public Func<bool>? CustomPasteHandler { get; set; }

    public ResizableRichTextBox()
    {
        DetectUrls = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PASTE)
        {
            // 커스텀 핸들러가 처리했으면 기본 paste 차단
            if (CustomPasteHandler != null && CustomPasteHandler())
                return;
        }

        base.WndProc(ref m);
    }
}
