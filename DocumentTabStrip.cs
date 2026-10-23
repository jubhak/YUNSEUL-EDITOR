namespace YunseulMemo;

/// <summary>
/// 에디터 상단에 표시되는 EditPlus 스타일 문서 탭 줄.
/// 파일 아이콘 + 이름, 선택 탭은 흰색으로 올라온 형태, 변경된 문서는 빨간 점(마우스를 올리면 ✕).
/// 가운데 클릭으로 닫기, 드래그로 순서 변경, 탭이 넘치면 좌우 화살표/휠 스크롤.
/// </summary>
public sealed class DocumentTabStrip : Control
{
    private sealed class Tab
    {
        public string Text = "";
        public string Tooltip = "";
        public bool Modified;
        public Color? Accent;
        public Rectangle Bounds;
    }

    private readonly List<Tab> _tabs = new();
    private readonly Dictionary<int, Bitmap> _iconCache = new();
    private readonly ToolTip _tip = new();
    private int _selected = -1;
    private int _hover = -1;
    private bool _hoverClose;
    private int _first;
    private bool _overflow;
    private Rectangle _leftBtn, _rightBtn;
    private int _dragIndex = -1;
    private Point _dragStart;
    private bool _dragging;

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler<int>? TabCloseRequested;
    public event EventHandler<(int From, int To)>? TabMoved;
    public event EventHandler<(int Index, Point Location)>? TabContextRequested;

    public DocumentTabStrip()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 24;
        Font = new Font("맑은 고딕", 9f);
        AllowDrop = true;
    }

    private int Dpi => Math.Max(96, DeviceDpi);
    private int S(int v) => (int)Math.Round(v * Dpi / 96.0);

    public int Count => _tabs.Count;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < -1 || value >= _tabs.Count || value == _selected) return;
            _selected = value;
            LayoutTabs();
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AddTab(string text, Color? accent = null, string tooltip = "")
    {
        _tabs.Add(new Tab { Text = text, Accent = accent, Tooltip = tooltip });
        LayoutTabs();
        Invalidate();
    }

    public void RemoveTabAt(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs.RemoveAt(index);
        if (index < _selected) _selected--;
        else if (_selected >= _tabs.Count) _selected = _tabs.Count - 1;
        _first = Math.Min(_first, Math.Max(0, _tabs.Count - 1));
        _hover = -1;
        LayoutTabs();
        Invalidate();
    }

    public void SetTab(int index, string text, bool modified, Color? accent = null, string? tooltip = null)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var t = _tabs[index];
        t.Text = text;
        t.Modified = modified;
        t.Accent = accent;
        if (tooltip != null) t.Tooltip = tooltip;
        LayoutTabs();
        Invalidate();
    }

    // ───────────── 레이아웃 ─────────────

    private int NaturalWidth(Tab t)
    {
        int tw = TextRenderer.MeasureText(t.Text, Font, new Size(int.MaxValue, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        int w = S(8) + S(16) + S(4) + tw + S(6) + S(14) + S(6);
        return Math.Clamp(w, S(90), S(220));
    }

    private void LayoutTabs()
    {
        if (Width <= 0) return;
        int pad = S(2);
        int btnW = S(16);
        int total = 0;
        foreach (var t in _tabs) total += NaturalWidth(t);

        _overflow = total > Width - pad * 2;
        int avail = Width - pad * 2 - (_overflow ? btnW * 2 : 0);

        if (_overflow)
        {
            _rightBtn = new Rectangle(Width - btnW, 1, btnW, Height - 2);
            _leftBtn = new Rectangle(Width - btnW * 2, 1, btnW, Height - 2);
        }
        else
        {
            _first = 0;
            _leftBtn = _rightBtn = Rectangle.Empty;
        }

        // 선택된 탭이 보이도록 시작 탭 조정
        if (_overflow && _selected >= 0)
        {
            if (_selected < _first) _first = _selected;
            while (_first < _selected && WidthRange(_first, _selected) > avail) _first++;
        }
        _first = Math.Clamp(_first, 0, Math.Max(0, _tabs.Count - 1));

        int x = pad;
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (i < _first) { _tabs[i].Bounds = Rectangle.Empty; continue; }
            int w = NaturalWidth(_tabs[i]);
            _tabs[i].Bounds = new Rectangle(x, 0, w, Height);
            x += w;
        }
    }

    private int WidthRange(int from, int to)
    {
        int w = 0;
        for (int i = from; i <= to && i < _tabs.Count; i++) w += NaturalWidth(_tabs[i]);
        return w;
    }

    private Rectangle CloseRect(Rectangle tab)
    {
        int s = S(14);
        return new Rectangle(tab.Right - s - S(6), tab.Top + (tab.Height - s) / 2 + S(1), s, s);
    }

    private int HitTab(Point p)
    {
        int limit = _overflow ? _leftBtn.Left : Width;
        if (p.X >= limit) return -1;
        for (int i = _first; i < _tabs.Count; i++)
            if (_tabs[i].Bounds.Contains(p)) return i;
        return -1;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutTabs();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutTabs();
        Invalidate();
    }

    // ───────────── 마우스 ─────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragIndex >= 0 && e.Button == MouseButtons.Left)
        {
            if (!_dragging && Math.Abs(e.X - _dragStart.X) > S(6)) _dragging = true;
            if (_dragging)
            {
                int target = HitTab(e.Location);
                if (target >= 0 && target != _dragIndex)
                {
                    var t = _tabs[_dragIndex];
                    _tabs.RemoveAt(_dragIndex);
                    _tabs.Insert(target, t);
                    int from = _dragIndex;
                    _dragIndex = target;
                    _selected = target;
                    LayoutTabs();
                    Invalidate();
                    TabMoved?.Invoke(this, (from, target));
                }
                return;
            }
        }

        int h = HitTab(e.Location);
        bool hc = h >= 0 && CloseRect(_tabs[h].Bounds).Contains(e.Location);
        if (h != _hover || hc != _hoverClose)
        {
            if (h != _hover) _tip.SetToolTip(this, h >= 0 ? _tabs[h].Tooltip : "");
            _hover = h;
            _hoverClose = hc;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1 || _hoverClose) { _hover = -1; _hoverClose = false; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_overflow && e.Button == MouseButtons.Left)
        {
            if (_leftBtn.Contains(e.Location)) { _first = Math.Max(0, _first - 1); LayoutTabs(); Invalidate(); return; }
            if (_rightBtn.Contains(e.Location)) { _first = Math.Min(_tabs.Count - 1, _first + 1); LayoutTabs(); Invalidate(); return; }
        }

        int i = HitTab(e.Location);
        if (i < 0) return;

        if (e.Button == MouseButtons.Middle) { TabCloseRequested?.Invoke(this, i); return; }

        if (e.Button == MouseButtons.Left)
        {
            if (CloseRect(_tabs[i].Bounds).Contains(e.Location) && (i == _selected || i == _hover))
            {
                TabCloseRequested?.Invoke(this, i);
                return;
            }
            SelectedIndex = i;
            _dragIndex = i;
            _dragStart = e.Location;
            _dragging = false;
        }
        else if (e.Button == MouseButtons.Right)
        {
            SelectedIndex = i;
            TabContextRequested?.Invoke(this, (i, e.Location));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragIndex = -1;
        _dragging = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        // 빈 영역 더블 클릭은 MainForm이 처리하지 않으므로 무시
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_overflow) return;
        _first = Math.Clamp(_first + (e.Delta > 0 ? -1 : 1), 0, Math.Max(0, _tabs.Count - 1));
        LayoutTabs();
        Invalidate();
    }

    // ───────────── 그리기 ─────────────

    private Bitmap Icon(Color? accent)
    {
        int key = accent?.ToArgb() ?? 0;
        if (!_iconCache.TryGetValue(key, out var bmp))
        {
            bmp = ToolIcons.Create(Ico.Page, S(16), accent);
            _iconCache[key] = bmp;
        }
        return bmp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(SystemColors.Control);

        using var line = new Pen(Color.FromArgb(160, 160, 160));
        g.DrawLine(line, 0, Height - 1, Width, Height - 1);

        int clipRight = _overflow ? _leftBtn.Left : Width;
        g.SetClip(new Rectangle(0, 0, clipRight, Height));

        // 비선택 탭 먼저, 선택 탭을 마지막에 그려서 위에 올라오게 함
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = _first; i < _tabs.Count; i++)
            {
                bool sel = i == _selected;
                if ((pass == 1) != sel) continue;
                var t = _tabs[i];
                if (t.Bounds.Left >= clipRight) break;
                DrawTab(g, t, i, sel);
            }
        }
        g.ResetClip();

        if (_overflow) DrawArrows(g);
    }

    private void DrawTab(Graphics g, Tab t, int index, bool sel)
    {
        var r = t.Bounds;
        int top = sel ? S(1) : S(3);
        var body = new Rectangle(r.Left, top, r.Width - 1, Height - top);

        using (var br = new SolidBrush(sel ? Color.White : Color.FromArgb(224, 224, 224)))
            g.FillRectangle(br, body);

        using (var pen = new Pen(sel ? Color.FromArgb(130, 130, 130) : Color.FromArgb(170, 170, 170)))
        {
            g.DrawLine(pen, body.Left, body.Bottom, body.Left, body.Top);
            g.DrawLine(pen, body.Left, body.Top, body.Right, body.Top);
            g.DrawLine(pen, body.Right, body.Top, body.Right, body.Bottom);
        }

        if (sel)
        {
            // 선택 탭 위쪽 강조선 + 아래 구분선 지우기
            using var accent = new Pen(Color.FromArgb(60, 120, 210), Math.Max(1, S(2)));
            g.DrawLine(accent, body.Left + 1, body.Top + 1, body.Right - 1, body.Top + 1);
            using var white = new Pen(Color.White);
            g.DrawLine(white, body.Left + 1, Height - 1, body.Right - 1, Height - 1);
        }

        int cy = top + (Height - top) / 2;
        var icon = Icon(t.Accent);
        g.DrawImage(icon, r.Left + S(6), cy - icon.Height / 2, icon.Width, icon.Height);

        var textRect = new Rectangle(r.Left + S(6) + S(16) + S(4), top, r.Width - S(6) - S(16) - S(4) - S(22), Height - top);
        TextRenderer.DrawText(g, t.Text, Font, textRect, sel ? Color.Black : Color.FromArgb(70, 70, 70),
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left);

        // 닫기 버튼 / 수정 표시
        var cr = CloseRect(r);
        bool hotTab = index == _hover || sel;
        bool hotClose = index == _hover && _hoverClose;
        if (hotClose)
        {
            using var br = new SolidBrush(Color.FromArgb(220, 200, 200));
            g.FillRectangle(br, cr);
        }
        if (hotTab || t.Modified)
        {
            if (t.Modified && !hotClose && !(index == _hover))
            {
                int d = S(8);
                using var br = new SolidBrush(Color.FromArgb(210, 60, 50));
                var sm = g.SmoothingMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(br, cr.Left + (cr.Width - d) / 2, cr.Top + (cr.Height - d) / 2, d, d);
                g.SmoothingMode = sm;
            }
            else
            {
                using var pen = new Pen(hotClose ? Color.FromArgb(190, 30, 30) : Color.FromArgb(110, 110, 110), 1.4f);
                int m = S(4);
                g.DrawLine(pen, cr.Left + m, cr.Top + m, cr.Right - m, cr.Bottom - m);
                g.DrawLine(pen, cr.Right - m, cr.Top + m, cr.Left + m, cr.Bottom - m);
            }
        }
    }

    private void DrawArrows(Graphics g)
    {
        using var br = new SolidBrush(Color.FromArgb(70, 70, 70));
        using var dis = new SolidBrush(Color.FromArgb(180, 180, 180));
        bool canLeft = _first > 0;
        bool canRight = _tabs.Count > 0 && _tabs[^1].Bounds.Right > _leftBtn.Left;
        int s = S(4);
        var l = _leftBtn; var r = _rightBtn;
        g.FillPolygon(canLeft ? br : dis, new[] { new Point(l.Left + l.Width / 2 + s / 2, l.Top + l.Height / 2 - s), new Point(l.Left + l.Width / 2 + s / 2, l.Top + l.Height / 2 + s), new Point(l.Left + l.Width / 2 - s / 2, l.Top + l.Height / 2) });
        g.FillPolygon(canRight ? br : dis, new[] { new Point(r.Left + r.Width / 2 - s / 2, r.Top + r.Height / 2 - s), new Point(r.Left + r.Width / 2 - s / 2, r.Top + r.Height / 2 + s), new Point(r.Left + r.Width / 2 + s / 2, r.Top + r.Height / 2) });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var b in _iconCache.Values) b.Dispose();
            _iconCache.Clear();
            _tip.Dispose();
        }
        base.Dispose(disposing);
    }
}
