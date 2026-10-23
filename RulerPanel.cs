namespace YunseulMemo;

/// <summary>
/// EditPlus 스타일 칸(column) 눈금자.
/// 편집 영역의 가로 스크롤과 동기화되며, 캐럿 위치를 파란 표시로 보여줍니다.
/// 칸 폭은 에디터 글꼴의 '0' 폭 기준이므로 고정폭 글꼴에서 정확합니다.
/// </summary>
public class RulerPanel : Control
{
    private RichTextBox? _editor;
    private int _leftOffset;

    public RulerPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 18;
        Font = new Font("Segoe UI", 7f);
    }

    /// <summary>눈금자 왼쪽 끝에서 에디터 클라이언트 영역 시작점까지의 거리(거터 폭 + 테두리).</summary>
    public int LeftOffset
    {
        get => _leftOffset;
        set { if (_leftOffset != value) { _leftOffset = value; Invalidate(); } }
    }

    public void AttachEditor(RichTextBox? editor)
    {
        if (_editor != null)
        {
            _editor.HScroll -= OnChanged;
            _editor.VScroll -= OnChanged;
            _editor.SelectionChanged -= OnChanged;
            _editor.TextChanged -= OnChanged;
            _editor.Resize -= OnChanged;
            _editor.FontChanged -= OnChanged;
        }
        _editor = editor;
        if (_editor != null)
        {
            _editor.HScroll += OnChanged;
            _editor.VScroll += OnChanged;
            _editor.SelectionChanged += OnChanged;
            _editor.TextChanged += OnChanged;
            _editor.Resize += OnChanged;
            _editor.FontChanged += OnChanged;
        }
        Invalidate();
    }

    private void OnChanged(object? s, EventArgs e) => Invalidate();

    private int S(int v) => (int)Math.Round(v * Math.Max(96, DeviceDpi) / 96.0);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.FromArgb(252, 252, 246));
        using var border = new Pen(Color.FromArgb(160, 160, 160));
        g.DrawLine(border, 0, Height - 1, Width, Height - 1);

        // 거터 위 영역은 회색으로 비움
        using (var gb = new SolidBrush(Color.FromArgb(238, 238, 238)))
            g.FillRectangle(gb, 0, 0, Math.Max(0, _leftOffset), Height - 1);
        g.DrawLine(border, Math.Max(0, _leftOffset - 1), 0, Math.Max(0, _leftOffset - 1), Height);

        if (_editor == null) return;

        float cw = CharWidth(_editor.Font);
        if (cw < 2) return;

        // 첫 글자의 화면 X: 가로 스크롤 만큼 음수로 이동
        float origin = _leftOffset;
        try { origin += _editor.GetPositionFromCharIndex(0).X; } catch { }

        var clip = new Rectangle(_leftOffset, 0, Math.Max(0, Width - _leftOffset), Height);
        g.SetClip(clip);

        int first = Math.Max(0, (int)Math.Floor((_leftOffset - origin) / cw));
        int last = (int)Math.Ceiling((Width - origin) / cw) + 1;

        using var tick = new Pen(Color.FromArgb(90, 90, 90));
        using var txt = new SolidBrush(Color.FromArgb(40, 40, 40));
        int h = Height - 1;
        for (int c = first; c <= last; c++)
        {
            int col = c + 1;                       // 1-based 칸 번호
            float x = origin + c * cw + cw / 2f;   // 글자 칸의 가운데
            int len = col % 10 == 0 ? S(7) : col % 5 == 0 ? S(5) : S(2);
            g.DrawLine(tick, x, h - len, x, h - 1);

            if (col % 10 == 0)
            {
                string label = col.ToString();
                var sz = g.MeasureString(label, Font);
                g.DrawString(label, Font, txt, x - sz.Width / 2f, 0);
            }
        }

        // 캐럿 위치 표시
        try
        {
            var p = _editor.GetPositionFromCharIndex(Math.Min(_editor.SelectionStart, _editor.TextLength));
            float cx = _leftOffset + p.X;
            using var caret = new SolidBrush(Color.FromArgb(40, 90, 210));
            g.FillPolygon(caret, new[] { new PointF(cx - 3, h - 5), new PointF(cx + 3, h - 5), new PointF(cx, h - 1) });
        }
        catch { }
        g.ResetClip();
    }

    private static float CharWidth(Font f)
    {
        var sz = TextRenderer.MeasureText("0000000000", f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        return sz.Width / 10f;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) AttachEditor(null);
        base.Dispose(disposing);
    }
}
