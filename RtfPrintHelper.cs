using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace YunseulMemo;

/// <summary>RichTextBox EM_FORMATRANGE 기반 서식 인쇄.</summary>
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

    public static void PrintPage(RichTextBox editor, PrintPageEventArgs e, ref int charIndex)
    {
        if (e.Graphics == null) return;

        float dpiX = e.Graphics.DpiX;
        float dpiY = e.Graphics.DpiY;

        var margin = e.MarginBounds;
        var page = e.PageBounds;

        var fmt = new FORMATRANGE
        {
            chrg = new CHARRANGE { cpMin = charIndex, cpMax = editor.TextLength },
            rc = ToTwips(margin, dpiX, dpiY),
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
