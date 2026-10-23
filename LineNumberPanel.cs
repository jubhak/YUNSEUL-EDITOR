namespace YunseulMemo;

/// <summary>
/// RichTextBox 좌측 줄 번호 거터 (EditPlus 스타일: 연한 회색 바탕 + 우측 구분선).
///
/// - 자동 줄 바꿈이 켜져 있어도 "논리 줄" 번호를 표시합니다 (이어지는 화면 줄에는 번호 없음).
/// - 줄 시작 위치 인덱스를 캐시해 상태 표시줄의 줄/칸 계산에도 재사용합니다.
/// - 북마크(파란 마커) 표시, 거터 클릭으로 줄 선택.
/// </summary>
public class LineNumberPanel : Control
{
    private RichTextBox? _editor;
    private string _text = "";
    private readonly List<int> _lineStarts = new() { 0 };
    private bool _dirty = true;
    private int _lastLineCount = 1;
    private HashSet<int> _bookmarks = new();

    private static readonly Color BackCol = Color.FromArgb(238, 238, 238);
    private static readonly Color LineCol = Color.FromArgb(170, 170, 170);
    private static readonly Color NumCol = Color.FromArgb(110, 110, 110);
    private static readonly Color CurCol = Color.FromArgb(0, 0, 0);

    private int _calcWidth = 0;

    public LineNumberPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Width = 48;
    }

    /// <summary>북마크가 바뀌었을 때(토글/이동 보정) 발생.</summary>
    public event EventHandler? BookmarksChanged;

    private int Dpi => Math.Max(96, DeviceDpi);
    private int S(int v) => (int)Math.Round(v * Dpi / 96.0);

    public void AttachEditor(RichTextBox? editor, HashSet<int>? bookmarks = null)
    {
        if (_editor != null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.VScroll -= OnEditorScroll;
            _editor.SelectionChanged -= OnEditorSelection;
            _editor.Resize -= OnEditorScroll;
            _editor.FontChanged -= OnEditorFontChanged;
        }

        _editor = editor;
        _bookmarks = bookmarks ?? new HashSet<int>();
        _dirty = true;

        if (_editor != null)
        {
            _editor.TextChanged += OnEditorTextChanged;
            _editor.VScroll += OnEditorScroll;
            _editor.SelectionChanged += OnEditorSelection;
            _editor.Resize += OnEditorScroll;
            _editor.FontChanged += OnEditorFontChanged;
            EnsureIndex();
            _lastLineCount = _lineStarts.Count;
        }

        RecalculateWidth();
        Invalidate();
    }

    public void DetachEditor() => AttachEditor(null);

    // ───────────── 줄 인덱스 ─────────────

    private void EnsureIndex()
    {
        if (!_dirty) return;
        _dirty = false;
        _lineStarts.Clear();
        _lineStarts.Add(0);
        _text = _editor?.Text ?? "";
        for (int i = 0; i < _text.Length; i++)
            if (_text[i] == '\n') _lineStarts.Add(i + 1);
    }

    /// <summary>논리 줄 수.</summary>
    public int LineCount { get { EnsureIndex(); return _lineStarts.Count; } }

    /// <summary>문자 인덱스가 속한 논리 줄 (0-based).</summary>
    public int GetLineFromChar(int charIndex)
    {
        EnsureIndex();
        int idx = _lineStarts.BinarySearch(charIndex);
        return idx >= 0 ? idx : ~idx - 1;
    }

    /// <summary>논리 줄의 첫 문자 인덱스.</summary>
    public int GetLineStart(int line)
    {
        EnsureIndex();
        line = Math.Clamp(line, 0, _lineStarts.Count - 1);
        return _lineStarts[line];
    }

    /// <summary>논리 줄의 길이 (개행 제외).</summary>
    public int GetLineLength(int line)
    {
        EnsureIndex();
        line = Math.Clamp(line, 0, _lineStarts.Count - 1);
        int start = _lineStarts[line];
        int end = line + 1 < _lineStarts.Count ? _lineStarts[line + 1] - 1 : _text.Length;
        return Math.Max(0, end - start);
    }

    /// <summary>캐시된 텍스트에서 문자 하나를 가져옵니다 (범위를 벗어나면 null).</summary>
    public char? GetCharAt(int charIndex)
    {
        EnsureIndex();
        return charIndex >= 0 && charIndex < _text.Length ? _text[charIndex] : null;
    }

    /// <summary>캐시된 텍스트의 일부를 반환합니다 (범위는 자동 보정).</summary>
    public string GetRange(int start, int length)
    {
        EnsureIndex();
        start = Math.Clamp(start, 0, _text.Length);
        length = Math.Clamp(length, 0, _text.Length - start);
        return _text.Substring(start, length);
    }

    // ───────────── 북마크 ─────────────

    public IReadOnlyCollection<int> Bookmarks => _bookmarks;

    public void ToggleBookmark(int line)
    {
        if (!_bookmarks.Remove(line)) _bookmarks.Add(line);
        Invalidate();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearBookmarks()
    {
        if (_bookmarks.Count == 0) return;
        _bookmarks.Clear();
        Invalidate();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>현재 줄 기준으로 다음/이전 북마크 줄을 찾습니다 (끝에 도달하면 순환). 없으면 -1.</summary>
    public int FindBookmark(int fromLine, bool forward)
    {
        if (_bookmarks.Count == 0) return -1;
        var sorted = _bookmarks.OrderBy(x => x).ToList();
        if (forward)
        {
            foreach (var b in sorted) if (b > fromLine) return b;
            return sorted[0];
        }
        for (int i = sorted.Count - 1; i >= 0; i--) if (sorted[i] < fromLine) return sorted[i];
        return sorted[^1];
    }

    /// <summary>줄 수가 변했을 때 편집 위치를 기준으로 북마크 줄 번호를 보정합니다.</summary>
    private void AdjustBookmarks(int delta)
    {
        if (_bookmarks.Count == 0 || _editor == null) return;
        int caretLine = GetLineFromChar(_editor.SelectionStart);
        var result = new HashSet<int>();
        foreach (int b in _bookmarks)
        {
            int nb = b;
            if (delta > 0)
            {
                if (b > caretLine - delta) nb = b + delta;
            }
            else
            {
                int removed = -delta;
                if (b > caretLine + removed) nb = b + delta;
                else if (b > caretLine) continue; // 삭제된 줄의 북마크는 제거
            }
            result.Add(Math.Max(0, nb));
        }
        _bookmarks.Clear();
        foreach (int b in result) _bookmarks.Add(b);
    }

    // ───────────── 이벤트 ─────────────

    private void OnEditorTextChanged(object? s, EventArgs e)
    {
        _dirty = true;
        EnsureIndex();
        int count = _lineStarts.Count;
        if (count != _lastLineCount)
        {
            AdjustBookmarks(count - _lastLineCount);
            _lastLineCount = count;
        }
        RecalculateWidth();
        Invalidate();
    }

    private void OnEditorScroll(object? s, EventArgs e) => Invalidate();
    private void OnEditorSelection(object? s, EventArgs e) => Invalidate();
    private void OnEditorFontChanged(object? s, EventArgs e) { RecalculateWidth(); Invalidate(); }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RecalculateWidth();
        Invalidate();
    }

    private void RecalculateWidth()
    {
        if (_editor == null) return;
        EnsureIndex();
        int digits = Math.Max(3, _lineStarts.Count.ToString().Length);
        int digitW = TextRenderer.MeasureText("0", Font, new Size(100, 100), TextFormatFlags.NoPadding).Width;
        int w = S(14) + digitW * digits + S(6);   // 북마크 마커 영역 + 숫자 + 여백
        if (w != _calcWidth)
        {
            _calcWidth = w;
            Width = w;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_editor == null || e.Button != MouseButtons.Left) return;

        int charIdx = _editor.GetCharIndexFromPosition(new Point(2, Math.Max(0, e.Y)));
        int line = GetLineFromChar(charIdx);
        int start = GetLineStart(line);
        int len = GetLineLength(line);
        int next = line + 1 < LineCount ? 1 : 0; // 개행 포함해서 선택
        _editor.Focus();
        _editor.Select(start, len + next);
    }

    // ───────────── 그리기 ─────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using (var bg = new SolidBrush(BackCol)) g.FillRectangle(bg, ClientRectangle);
        using (var pen = new Pen(LineCol)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

        if (_editor == null) return;
        EnsureIndex();

        int currentLine = GetLineFromChar(_editor.SelectionStart);
        int firstChar = _editor.GetCharIndexFromPosition(new Point(0, 0));
        int visual = _editor.GetLineFromCharIndex(firstChar);
        int lastVisual = _editor.GetLineFromCharIndex(_editor.TextLength);

        using var boldFont = new Font(Font, FontStyle.Bold);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.Right | TextFormatFlags.NoPrefix;

        for (int v = visual; v <= lastVisual; v++)
        {
            int ci = _editor.GetFirstCharIndexFromLine(v);
            if (ci < 0) break;
            int y = _editor.GetPositionFromCharIndex(ci).Y;
            if (y > ClientSize.Height) break;
            if (y < -_editor.Font.Height * 2) continue;

            // 이 화면 줄이 논리 줄의 첫 줄일 때만 번호 표시 (줄 바꿈으로 이어진 줄은 건너뜀)
            int logical = _lineStarts.BinarySearch(ci);
            if (logical < 0) continue;

            bool isCur = logical == currentLine;
            var rect = new Rectangle(S(14), y, Width - S(14) - S(5), _editor.Font.Height + 2);
            TextRenderer.DrawText(g, (logical + 1).ToString(), isCur ? boldFont : Font, rect, isCur ? CurCol : NumCol, flags);

            if (_bookmarks.Contains(logical))
            {
                int mh = Math.Min(S(10), _editor.Font.Height - 2);
                var mr = new Rectangle(S(2), y + (_editor.Font.Height - mh) / 2 + 1, S(10), mh);
                using var br = new SolidBrush(Color.FromArgb(50, 110, 220));
                using var pen = new Pen(Color.FromArgb(20, 60, 140));
                g.FillRectangle(br, mr);
                g.DrawRectangle(pen, mr);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DetachEditor();
        base.Dispose(disposing);
    }
}
