using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace YunseulMemo;

/// <summary>표 서식 및 스타일 옵션.</summary>
public sealed class TableStyleOptions
{
    public int Rows { get; set; } = 3;
    public int Cols { get; set; } = 3;

    /// <summary>선 굵기 (0: 투명선/없음, 1: 1pt, 2: 2pt, 3: 3pt).</summary>
    public int BorderWidth { get; set; } = 1;

    /// <summary>선 색상.</summary>
    public Color BorderColor { get; set; } = Color.FromArgb(203, 213, 225);

    /// <summary>투명선 여부 (선 숨기기).</summary>
    public bool IsTransparentBorder { get; set; } = false;

    /// <summary>각 컬럼별 정렬.</summary>
    public HorizontalAlignment[] Alignments { get; set; } = [];

    /// <summary>첫 번째 행 헤더 스타일 적용 여부.</summary>
    public bool HasHeaderRow { get; set; } = true;

    /// <summary>헤더 배경 색상.</summary>
    public Color HeaderBgColor { get; set; } = Color.FromArgb(241, 245, 249);

    /// <summary>표 전체 너비 (twips 단위, 기본 7200 = 약 5인치).</summary>
    public int TotalWidthTwips { get; set; } = 7500;
}

/// <summary>RTF 표(Table) 생성 및 서식/데이터 관리 헬퍼.</summary>
internal static class TableHelper
{
    /// <summary>표 스타일 옵션을 기반으로 표준 RTF 표 문자열을 생성합니다.</summary>
    public static string GenerateTableRtf(TableStyleOptions opt, string[,]? cellData = null)
    {
        int rows = Math.Max(1, opt.Rows);
        int cols = Math.Max(1, opt.Cols);
        int colWidth = Math.Max(720, opt.TotalWidthTwips / cols);

        var aligns = new HorizontalAlignment[cols];
        for (int c = 0; c < cols; c++)
        {
            aligns[c] = (c < opt.Alignments.Length) ? opt.Alignments[c] : HorizontalAlignment.Left;
        }

        var sb = new StringBuilder();

        // 1. RTF 헤더 및 폰트/컬러 테이블
        sb.Append(@"{\rtf1\ansi\ansicpg949\deff0");
        sb.Append(@"{\fonttbl{\f0\fnil\fcharset129 맑은 고딕;}}");

        // 컬러 테이블 (1: 테두리색, 2: 헤더배경색)
        Color bc = opt.BorderColor;
        Color hc = opt.HeaderBgColor;
        sb.Append($@"{{\colortbl ;\red{bc.R}\green{bc.G}\blue{bc.B};\red{hc.R}\green{hc.G}\blue{hc.B};}}");
        sb.AppendLine();

        // 2. 각 행 구성
        for (int r = 0; r < rows; r++)
        {
            bool isHeader = (r == 0 && opt.HasHeaderRow);

            // 행 정의 (\trowd)
            sb.Append(@"\trowd\trgaph120\trleft0");

            // 각 셀의 테두리 및 너비 정의
            for (int c = 0; c < cols; c++)
            {
                if (opt.IsTransparentBorder || opt.BorderWidth <= 0)
                {
                    sb.Append(@"\clbrdrt\brdrnone\clbrdrl\brdrnone\clbrdrb\brdrnone\clbrdrr\brdrnone");
                }
                else
                {
                    int w = Math.Clamp(opt.BorderWidth * 15, 15, 60); // 15 twips = 1pt
                    sb.Append($@"\clbrdrt\brdrs\brdrw{w}\brdrcf1\clbrdrl\brdrs\brdrw{w}\brdrcf1\clbrdrb\brdrs\brdrw{w}\brdrcf1\clbrdrr\brdrs\brdrw{w}\brdrcf1");
                }

                if (isHeader)
                {
                    sb.Append(@"\clcbpat2"); // 헤더 배경색
                }

                int rightEdge = (c + 1) * colWidth;
                sb.Append($@"\cellx{rightEdge}");
            }

            sb.AppendLine();

            // 셀 텍스트 내용 삽입
            for (int c = 0; c < cols; c++)
            {
                string alignCmd = aligns[c] switch
                {
                    HorizontalAlignment.Center => @"\qc",
                    HorizontalAlignment.Right => @"\qr",
                    _ => @"\ql"
                };

                string content;
                if (cellData != null && r < cellData.GetLength(0) && c < cellData.GetLength(1))
                {
                    content = cellData[r, c] ?? "";
                }
                else if (isHeader)
                {
                    content = $"열 {c + 1}";
                }
                else
                {
                    content = " ";
                }

                string safeContent = RtfHelper.EscapeRtfText(content);
                if (isHeader)
                {
                    sb.Append($@"\intbl{alignCmd}\b {safeContent}\b0\cell");
                }
                else
                {
                    sb.Append($@"\intbl{alignCmd} {safeContent}\cell");
                }
            }

            // 행 닫기
            sb.AppendLine(@"\row");
        }

        // 표 종료 및 새 단락
        sb.AppendLine(@"\pard\par}");
        return sb.ToString();
    }

    /// <summary>RichTextBox에 멋진 RTF 표를 삽입합니다.</summary>
    public static void InsertTable(RichTextBox editor, TableStyleOptions opt, string[,]? cellData = null)
    {
        string rtf = GenerateTableRtf(opt, cellData);
        RtfHelper.PasteRtf(editor, rtf);
    }

    /// <summary>현재 선택 영역 또는 캐럿 위치가 표 내부인지 검사합니다.</summary>
    public static bool IsCursorInTable(RichTextBox editor)
    {
        if (editor == null) return false;

        // 선택 영역의 RTF 검사
        if (!string.IsNullOrEmpty(editor.SelectedRtf) &&
            (editor.SelectedRtf.Contains(@"\trowd") || editor.SelectedRtf.Contains(@"\intbl")))
        {
            return true;
        }

        // 캐럿 주변 단락 RTF 검사
        try
        {
            int savedStart = editor.SelectionStart;
            int savedLen = editor.SelectionLength;

            int line = editor.GetLineFromCharIndex(savedStart);
            int lineStart = editor.GetFirstCharIndexFromLine(line);
            int lineLen = editor.Lines.Length > line ? editor.Lines[line].Length : 0;

            if (lineStart >= 0 && lineLen >= 0)
            {
                editor.Select(lineStart, Math.Max(1, lineLen));
                string lineRtf = editor.SelectedRtf ?? "";
                editor.Select(savedStart, savedLen);

                if (lineRtf.Contains(@"\trowd") || lineRtf.Contains(@"\intbl"))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// 현재 캐럿이 위치한 표 전체 영역을 자동으로 선택합니다.
    /// 성공 시 true, 실패 시 false를 반환합니다.
    /// </summary>
    public static bool TrySelectTableAtCursor(RichTextBox editor, out (int Start, int Length) range)
    {
        range = (0, 0);
        if (editor == null) return false;

        int origStart = editor.SelectionStart;
        int origLen = editor.SelectionLength;

        // 현재 위치에서 위아래로 라인을 확장하며 표의 시작(\trowd)과 끝을 탐색
        int curLine = editor.GetLineFromCharIndex(origStart);
        int totalLines = editor.Lines.Length;

        int startLine = curLine;
        while (startLine > 0)
        {
            int ls = editor.GetFirstCharIndexFromLine(startLine);
            int ll = editor.Lines[startLine].Length;
            editor.Select(ls, Math.Max(1, ll));
            if (!editor.SelectedRtf.Contains(@"\intbl") && !editor.SelectedRtf.Contains(@"\trowd"))
            {
                startLine++;
                break;
            }
            startLine--;
        }
        startLine = Math.Clamp(startLine, 0, totalLines - 1);

        int endLine = curLine;
        while (endLine < totalLines)
        {
            int ls = editor.GetFirstCharIndexFromLine(endLine);
            int ll = editor.Lines[endLine].Length;
            editor.Select(ls, Math.Max(1, ll));
            if (!editor.SelectedRtf.Contains(@"\intbl") && !editor.SelectedRtf.Contains(@"\trowd"))
            {
                endLine--;
                break;
            }
            endLine++;
        }
        endLine = Math.Clamp(endLine, startLine, totalLines - 1);

        int tableCharStart = editor.GetFirstCharIndexFromLine(startLine);
        int tableCharEnd = editor.GetFirstCharIndexFromLine(endLine) + editor.Lines[endLine].Length;
        int length = Math.Max(1, tableCharEnd - tableCharStart);

        editor.Select(tableCharStart, length);
        string tableRtf = editor.SelectedRtf ?? "";
        if (tableRtf.Contains(@"\intbl") || tableRtf.Contains(@"\trowd"))
        {
            range = (tableCharStart, length);
            return true;
        }

        // 복원
        editor.Select(origStart, origLen);
        return false;
    }

    /// <summary>선택된 표 RTF로부터 셀 텍스트 데이터를 추출합니다.</summary>
    public static string[,]? ParseTableCells(string rtf, out int outRows, out int outCols)
    {
        outRows = 0;
        outCols = 0;
        if (string.IsNullOrWhiteSpace(rtf) || !rtf.Contains(@"\row")) return null;

        var rowMatches = Regex.Matches(rtf, @"\\trowd.*?(?=\\row)\\row", RegexOptions.Singleline);
        if (rowMatches.Count == 0) return null;

        var rowsData = new List<List<string>>();
        int maxCols = 0;

        foreach (Match rm in rowMatches)
        {
            string rowRtf = rm.Value;
            var cellMatches = Regex.Matches(rowRtf, @"\\intbl(.*?)\\cell", RegexOptions.Singleline);
            var cells = new List<string>();
            foreach (Match cm in cellMatches)
            {
                string cellText = CleanRtfText(cm.Groups[1].Value);
                cells.Add(cellText);
            }
            if (cells.Count > maxCols) maxCols = cells.Count;
            rowsData.Add(cells);
        }

        if (rowsData.Count == 0 || maxCols == 0) return null;

        outRows = rowsData.Count;
        outCols = maxCols;
        var result = new string[outRows, outCols];
        for (int r = 0; r < outRows; r++)
        {
            for (int c = 0; c < outCols; c++)
            {
                result[r, c] = (c < rowsData[r].Count) ? rowsData[r][c] : "";
            }
        }
        return result;
    }

    private static string CleanRtfText(string rtfSnippet)
    {
        // RTF 태그들(\qc, \b, \cf 등) 제거하고 순수 텍스트만 추출
        string text = Regex.Replace(rtfSnippet, @"\\[a-zA-Z]+(-?\d+)?\s?", " ");
        text = Regex.Replace(text, @"[{}]", "");
        return text.Trim();
    }
}
