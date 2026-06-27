namespace YunseulMemo;

/// <summary>
/// RichTextBox 좌측에 줄 번호를 표시하는 패널.
/// 줄 번호는 경계선 오른쪽(편집창 쪽)에 우측 정렬로 표시됩니다.
/// 줄 수가 늘어나면 자동으로 폭이 확장됩니다.
/// 현재 줄은 ▶ 마커와 함께 강조 표시됩니다.
/// </summary>
public class LineNumberPanel : Control
{
    private RichTextBox? _editor;
    private readonly Font _lineFont;
    private readonly Font _markerFont;
    private readonly Brush _lineBrush = new SolidBrush(Color.FromArgb(120, 120, 120));
    private readonly Brush _currentLineBrush = new SolidBrush(Color.FromArgb(30, 30, 140));
    private readonly Brush _bgBrush = new SolidBrush(Color.White);
    private readonly Brush _markerBrush = new SolidBrush(Color.FromArgb(30, 30, 140));

    private const int BorderX = 0; // 경계선은 왼쪽 끝에 그림
    private const int Padding_Right = 6; // 숫자 오른쪽 여백
    private const int Padding_Left = 4; // 마커 왼쪽 여백
    private int _lastCalculatedWidth = 52;

    public LineNumberPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Width = 52;
        _lineFont = new Font("Consolas", 10f);
        _markerFont = new Font("맑은 고딕", 7f);
    }

    public void AttachEditor(RichTextBox editor)
    {
        if (_editor != null)
        {
            _editor.TextChanged -= OnEditorChanged;
            _editor.VScroll -= OnEditorScroll;
            _editor.SelectionChanged -= OnEditorChanged;
            _editor.Resize -= OnEditorChanged;
        }

        _editor = editor;

        if (_editor != null)
        {
            _editor.TextChanged += OnEditorChanged;
            _editor.VScroll += OnEditorScroll;
            _editor.SelectionChanged += OnEditorChanged;
            _editor.Resize += OnEditorChanged;
        }

        RecalculateWidth();
        Invalidate();
    }

    public void DetachEditor()
    {
        AttachEditor(null!);
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        RecalculateWidth();
        Invalidate();
    }

    private void OnEditorScroll(object? sender, EventArgs e) => Invalidate();

    /// <summary>
    /// 줄 수에 따라 패널 폭을 자동 조정합니다.
    /// </summary>
    private void RecalculateWidth()
    {
        if (_editor == null) return;

        int totalLines = _editor.Lines.Length;
        if (totalLines == 0) totalLines = 1;

        // 최대 줄 번호의 자릿수 계산
        string maxLineStr = totalLines.ToString();
        int digitWidth;
        using (var g = CreateGraphics())
        {
            var size = g.MeasureString(maxLineStr + "W", _lineFont); // W for extra space
            digitWidth = (int)Math.Ceiling(size.Width);
        }

        // 마커(▶) 공간 + 숫자 공간 + 여백
        int newWidth = Padding_Left + 14 + digitWidth + Padding_Right;
        newWidth = Math.Max(newWidth, 44); // 최소 폭

        if (newWidth != _lastCalculatedWidth)
        {
            _lastCalculatedWidth = newWidth;
            Width = newWidth;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.FillRectangle(_bgBrush, ClientRectangle);

        // 왼쪽 경계선 (파일 브라우저와의 구분) - 그리지 않음, SplitContainer가 처리
        // 오른쪽에는 경계선 없음 - 편집창과 자연스럽게 연결

        if (_editor == null) return;

        int currentLine = _editor.GetLineFromCharIndex(_editor.SelectionStart);
        int firstCharIndex = _editor.GetCharIndexFromPosition(new Point(0, 0));
        int firstLine = _editor.GetLineFromCharIndex(firstCharIndex);

        int totalLines = _editor.Lines.Length;
        if (totalLines == 0) totalLines = 1;

        for (int i = firstLine; i < totalLines; i++)
        {
            int charIdx = _editor.GetFirstCharIndexFromLine(i);
            if (charIdx < 0) break;

            Point pos = _editor.GetPositionFromCharIndex(charIdx);
            int lineY = pos.Y;

            if (lineY > _editor.ClientSize.Height) break;
            if (lineY < -20) continue;

            bool isCurrent = (i == currentLine);
            string lineNum = (i + 1).ToString();
            var brush = isCurrent ? _currentLineBrush : _lineBrush;
            var font = _lineFont;

            var size = g.MeasureString(lineNum, font);
            // 우측 정렬: 패널 오른쪽 끝에서 Padding_Right만큼 떨어진 곳에 숫자 끝이 오도록
            float x = Width - size.Width - Padding_Right;
            float y = lineY + 1;

            g.DrawString(lineNum, font, brush, x, y);

            // Draw ▶ marker on current line
            if (isCurrent)
            {
                g.DrawString("▶", _markerFont, _markerBrush, Padding_Left, y + 2);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachEditor();
            _lineFont.Dispose();
            _markerFont.Dispose();
            _lineBrush.Dispose();
            _currentLineBrush.Dispose();
            _bgBrush.Dispose();
            _markerBrush.Dispose();
        }
        base.Dispose(disposing);
    }
}
