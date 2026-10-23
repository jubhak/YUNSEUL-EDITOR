using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace YunseulMemo;

/// <summary>
/// RichTextBox EM_FORMATRANGE 기반 서식 인쇄 및 머리말/꼬리말 렌더링.
/// </summary>
internal static class RtfPrintHelper
{
    private const int WM_USER = 0x0400;
    private const int EM_FORMATRANGE = WM_USER + 57;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CHARRANGE
    {
        public int cpMin;
        public int cpMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FORMATRANGE
    {
        public IntPtr hdc;
        public IntPtr hdcTarget;
        public RECT rc;
        public RECT rcPage;
        public CHARRANGE chrg;
    }

    public static void PrintPage(
        RichTextBox editor,
        PrintPageEventArgs e,
        ref int charIndex,
        int pageNumber = 1,
        string? headerText = null,
        string? footerText = null)
    {
        if (e.Graphics == null) return;

        float dpiX = e.Graphics.DpiX;
        float dpiY = e.Graphics.DpiY;

        var margin = e.MarginBounds;
        var page = e.PageBounds;

        int headerHeight = 0;
        int footerHeight = 0;

        // 1. 머리말 렌더링 (설정 시)
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            headerHeight = 28;
            using var headerFont = new Font("맑은 고딕", 8.5f, FontStyle.Regular);
            using var headerBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            var headerRect = new Rectangle(margin.Left, Math.Max(10, margin.Top - headerHeight), margin.Width, headerHeight - 4);
            e.Graphics.DrawString(headerText, headerFont, headerBrush, headerRect, sf);

            using var pen = new Pen(Color.FromArgb(203, 213, 225), 0.8f);
            e.Graphics.DrawLine(pen, margin.Left, margin.Top - 4, margin.Right, margin.Top - 4);
        }

        // 2. 꼬리말 렌더링 (설정 시)
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            footerHeight = 28;
            string processedFooter = footerText
                .Replace("[페이지]", pageNumber.ToString())
                .Replace("{PAGE}", pageNumber.ToString());

            using var footerFont = new Font("맑은 고딕", 8.5f, FontStyle.Regular);
            using var footerBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            var footerRect = new Rectangle(margin.Left, margin.Bottom + 4, margin.Width, footerHeight);
            e.Graphics.DrawString(processedFooter, footerFont, footerBrush, footerRect, sf);

            using var pen = new Pen(Color.FromArgb(203, 213, 225), 0.8f);
            e.Graphics.DrawLine(pen, margin.Left, margin.Bottom + 4, margin.Right, margin.Bottom + 4);
        }

        // 3. 본문 RTF 출력 영역 (머리말/꼬리말 공간 확보)
        var bodyMargin = new Rectangle(
            margin.Left,
            margin.Top + (headerHeight > 0 ? 6 : 0),
            margin.Width,
            Math.Max(50, margin.Height - (headerHeight > 0 ? 6 : 0) - (footerHeight > 0 ? 6 : 0))
        );

        var fmt = new FORMATRANGE
        {
            chrg = new CHARRANGE { cpMin = charIndex, cpMax = editor.TextLength },
            rc = ToTwips(bodyMargin, dpiX, dpiY),
            rcPage = ToTwips(page, dpiX, dpiY)
        };

        IntPtr hdc = e.Graphics.GetHdc();
        try
        {
            fmt.hdc = hdc;
            fmt.hdcTarget = hdc;

            IntPtr lParam = Marshal.AllocCoTaskMem(Marshal.SizeOf<FORMATRANGE>());
            try
            {
                Marshal.StructureToPtr(fmt, lParam, false);
                IntPtr result = SendMessage(editor.Handle, EM_FORMATRANGE, (IntPtr)1, lParam);
                charIndex = result.ToInt32();

                // 종료 신호 (wParam = 0)
                fmt.chrg.cpMin = 0;
                fmt.chrg.cpMax = 0;
                Marshal.StructureToPtr(fmt, lParam, false);
                SendMessage(editor.Handle, EM_FORMATRANGE, IntPtr.Zero, lParam);
            }
            finally
            {
                Marshal.FreeCoTaskMem(lParam);
            }
        }
        finally
        {
            e.Graphics.ReleaseHdc(hdc);
        }

        e.HasMorePages = charIndex < editor.TextLength;
    }

    private static RECT ToTwips(Rectangle bounds, float dpiX, float dpiY)
    {
        const float twipsPerInch = 1440f;
        return new RECT
        {
            Left = (int)(bounds.Left / dpiX * twipsPerInch),
            Top = (int)(bounds.Top / dpiY * twipsPerInch),
            Right = (int)(bounds.Right / dpiX * twipsPerInch),
            Bottom = (int)(bounds.Bottom / dpiY * twipsPerInch)
        };
    }
}
