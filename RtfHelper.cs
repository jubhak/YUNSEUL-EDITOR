using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace YunseulMemo;

/// <summary>RTF 하이퍼링크, HTML보내기, 클립보드 RTF 붙여넣기 유틸리티.</summary>
internal static class RtfHelper
{
    private static readonly Regex HyperlinkUrlRegex = new(
        @"HYPERLINK\s+""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void PasteRtf(RichTextBox editor, string rtf)
    {
        IDataObject? savedClipboard = null;
        try { savedClipboard = Clipboard.GetDataObject(); } catch { }

        try
        {
            Clipboard.SetData(DataFormats.Rtf, rtf);
            editor.Paste(DataFormats.GetFormat(DataFormats.Rtf));
        }
        finally
        {
            try
            {
                if (savedClipboard != null)
                    Clipboard.SetDataObject(savedClipboard, true);
            }
            catch { }
        }
    }

    public static string CreateHyperlinkRtf(string displayText, string url)
    {
        string safeUrl = url.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string safeText = EscapeRtfText(displayText);
        return $"{{\\rtf1\\ansi\\deff0{{\\field{{\\*\\fldinst{{HYPERLINK \"{safeUrl}\"}}}}{{\\fldrslt{{\\ul\\cf1 {safeText}}}}}}}}}";
    }

    public static void InsertHyperlink(RichTextBox editor, string displayText, string url)
    {
        PasteRtf(editor, CreateHyperlinkRtf(displayText, url));
    }

    public static string? ExtractHyperlinkUrl(RichTextBox editor, int cpMin, int cpMax)
    {
        int savedStart = editor.SelectionStart;
        int savedLen = editor.SelectionLength;

        try
        {
            int length = Math.Max(1, cpMax - cpMin);
            length = Math.Min(length, editor.TextLength - cpMin);
            if (cpMin < 0 || cpMin >= editor.TextLength) return null;

            editor.Select(cpMin, length);
            string rtf = editor.SelectedRtf ?? "";
            var match = HyperlinkUrlRegex.Match(rtf);
            if (match.Success) return match.Groups[1].Value;

            // 필드 경계가 잘렸을 수 있으므로 범위를 넓혀 재시도
            int expanded = Math.Min(editor.TextLength - cpMin, Math.Max(length, 64));
            editor.Select(cpMin, expanded);
            rtf = editor.SelectedRtf ?? "";
            match = HyperlinkUrlRegex.Match(rtf);
            return match.Success ? match.Groups[1].Value : null;
        }
        finally
        {
            editor.Select(savedStart, savedLen);
        }
    }

    public static bool TryOpenHyperlink(RichTextBox editor, int cpMin, int cpMax)
    {
        string? url = ExtractHyperlinkUrl(editor, cpMin, cpMax);
        if (string.IsNullOrWhiteSpace(url)) return false;

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ConvertToHtml(RichTextBox editor)
    {
        var body = new StringBuilder();
        int length = editor.TextLength;
        if (length == 0)
        {
            body.Append("<p></p>");
        }
        else
        {
            int runStart = 0;
            RunStyle style = ReadStyle(editor, 0);

            for (int i = 1; i <= length; i++)
            {
                RunStyle next = i < length ? ReadStyle(editor, i) : default;
                bool endOfRun = i == length || !style.Equals(next);

                if (endOfRun)
                {
                    string text = editor.Text.Substring(runStart, i - runStart);
                    AppendRun(body, text, style);
                    runStart = i;
                    style = next;
                }
            }
        }

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"ko\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<title>윤슬 에디터 문서</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:'맑은 고딕',sans-serif;max-width:800px;margin:40px auto;padding:20px;line-height:1.8;}");
        html.AppendLine("a{color:#0000ee;text-decoration:underline;}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.Append(body);
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static RunStyle ReadStyle(RichTextBox editor, int index)
    {
        editor.Select(index, 1);
        var font = editor.SelectionFont ?? editor.Font;
        string? url = ExtractHyperlinkUrl(editor, index, index + 1);

        return new RunStyle(
            font.FontFamily.Name,
            font.Size,
            font.Bold,
            font.Italic,
            font.Underline,
            font.Strikeout,
            editor.SelectionColor,
            editor.SelectionBackColor,
            url);
    }

    private static void AppendRun(StringBuilder body, string text, RunStyle style)
    {
        if (text.Length == 0) return;

        var encoded = new StringBuilder();
        foreach (char ch in text)
        {
            if (ch == '\r') continue;
            if (ch == '\n')
            {
                encoded.Append("<br>");
                continue;
            }
            encoded.Append(System.Net.WebUtility.HtmlEncode(ch.ToString()));
        }

        if (encoded.Length == 0) return;

        string inner = encoded.ToString();
        if (style.Bold) inner = $"<strong>{inner}</strong>";
        if (style.Italic) inner = $"<em>{inner}</em>";
        if (style.Underline) inner = $"<u>{inner}</u>";
        if (style.Strikeout) inner = $"<s>{inner}</s>";

        var css = new List<string>();
        if (!string.IsNullOrEmpty(style.FontFamily))
            css.Add($"font-family:'{style.FontFamily}',sans-serif");
        if (style.FontSize > 0)
            css.Add($"font-size:{style.FontSize:0.#}pt");
        if (style.ForeColor != Color.Empty && style.ForeColor != Color.Black)
            css.Add($"color:{ToHtmlColor(style.ForeColor)}");
        if (style.BackColor != Color.Empty && style.BackColor != Color.White)
            css.Add($"background-color:{ToHtmlColor(style.BackColor)}");

        if (css.Count > 0)
            inner = $"<span style=\"{string.Join(";", css)}\">{inner}</span>";

        if (!string.IsNullOrEmpty(style.Url))
        {
            string href = System.Net.WebUtility.HtmlEncode(style.Url);
            inner = $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\">{inner}</a>";
        }

        body.Append(inner);
    }

    private static string ToHtmlColor(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string EscapeRtfText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '{': sb.Append("\\{"); break;
                case '}': sb.Append("\\}"); break;
                case '\n': sb.Append("\\line "); break;
                case '\r': break;
                default:
                    if (ch < 128) sb.Append(ch);
                    else sb.Append($"\\u{(int)ch}?");
                    break;
            }
        }
        return sb.ToString();
    }

    private readonly struct RunStyle : IEquatable<RunStyle>
    {
        public string FontFamily { get; }
        public float FontSize { get; }
        public bool Bold { get; }
        public bool Italic { get; }
        public bool Underline { get; }
        public bool Strikeout { get; }
        public Color ForeColor { get; }
        public Color BackColor { get; }
        public string? Url { get; }

        public RunStyle(
            string fontFamily, float fontSize,
            bool bold, bool italic, bool underline, bool strikeout,
            Color foreColor, Color backColor, string? url)
        {
            FontFamily = fontFamily;
            FontSize = fontSize;
            Bold = bold;
            Italic = italic;
            Underline = underline;
            Strikeout = strikeout;
            ForeColor = foreColor;
            BackColor = backColor;
            Url = url;
        }

        public bool Equals(RunStyle other) =>
            FontFamily == other.FontFamily &&
            Math.Abs(FontSize - other.FontSize) < 0.01f &&
            Bold == other.Bold &&
            Italic == other.Italic &&
            Underline == other.Underline &&
            Strikeout == other.Strikeout &&
            ForeColor.ToArgb() == other.ForeColor.ToArgb() &&
            BackColor.ToArgb() == other.BackColor.ToArgb() &&
            string.Equals(Url, other.Url, StringComparison.OrdinalIgnoreCase);
    }
}
