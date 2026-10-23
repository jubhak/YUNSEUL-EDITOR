using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace YunseulMemo;

/// <summary>
/// 고급 그림 그리기 (스케치/서명/도형) 대화상자.
/// 팝업 창에 꽉 차는 반응형 캔버스, 세련된 직관적 아이콘 툴바, 툴팁, RGB/HEX/CMYK 색상 입력, 실행 취소/다시 실행을 지원합니다.
/// </summary>
public sealed class DrawingDialog : Form
{
    public enum DrawTool
    {
        Pen,         // 자유펜
        Highlighter, // 형광펜
        Eraser,      // 지우개
        Line,        // 직선
        Rectangle,   // 사각형
        Ellipse      // 원/타원
    }

    private readonly Panel _toolPanel;
    private readonly Panel _bottomPanel;
    private readonly PictureBox _canvasBox;
    private readonly Label _lblCanvasSize;
    private readonly ToolTip _toolTip = new() { InitialDelay = 200, ReshowDelay = 100, AutoPopDelay = 4000 };

    private Bitmap _canvasBitmap = null!;
    private Graphics _canvasGraphics = null!;

    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();

    private DrawTool _currentTool = DrawTool.Pen;
    private Color _currentColor = Color.FromArgb(15, 23, 42);
    private int _brushSize = 3;

    // 드로잉 상태
    private bool _isDrawing = false;
    private Point _startPoint;
    private Point _lastPoint;
    private Bitmap? _tempBitmap;

    private readonly Button _btnUndo;
    private readonly Button _btnRedo;
    private readonly Panel _pnlColorPreview;
    private readonly NumericUpDown _nudSize;
    private readonly List<(DrawTool tool, Button btn)> _toolButtons = new();

    /// <summary>완성된 그림 비트맵.</summary>
    public Image? ResultImage { get; private set; }

    public DrawingDialog()
    {
        Text = "그림 그리기 (스케치 / 서명 / 도형)";
        ClientSize = new Size(1100, 720); // 넉넉하고 시원한 기본 창 크기
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(800, 500);
        BackColor = Color.White;
        Font = new Font("맑은 고딕", 9f);
        DoubleBuffered = true;

        // 1. 상단 툴바 패널 (직관적인 예쁜 아이콘 툴바)
        _toolPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 8, 10, 8)
        };
        _toolPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, _toolPanel.Height - 1, _toolPanel.Width, _toolPanel.Height - 1);
        };

        int curX = 10;

        // 도구 버튼 (직관적인 아이콘 + 툴팁)
        CreateToolIconBtn("✏️", DrawTool.Pen, "자유펜 (P)\n자유로운 곡선 필기 및 서명", ref curX, isDefault: true);
        CreateToolIconBtn("🖍️", DrawTool.Highlighter, "형광펜 (H)\n반투명 강조 하이라이터", ref curX);
        CreateToolIconBtn("🧼", DrawTool.Eraser, "지우개 (E)\n배경 흰색으로 지우기", ref curX);
        CreateToolIconBtn("📏", DrawTool.Line, "직선 (L)\n반듯한 직선 그리기", ref curX);
        CreateToolIconBtn("⬜", DrawTool.Rectangle, "사각형 (R)\n직사각형 및 정사각형 그리기", ref curX);
        CreateToolIconBtn("⭕", DrawTool.Ellipse, "원/타원 (O)\n원 및 타원 그리기", ref curX);

        // 구분선 1
        AddSeparator(ref curX);

        // 굵기 스핀버튼
        var lblSize = new Label { Text = "굵기", Location = new Point(curX, 16), AutoSize = true, Font = new Font("맑은 고딕", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105) };
        _toolPanel.Controls.Add(lblSize);
        curX += 30;

        _nudSize = new NumericUpDown
        {
            Location = new Point(curX, 12),
            Width = 48,
            Minimum = 1,
            Maximum = 50,
            Value = _brushSize,
            Font = new Font("Segoe UI", 9f)
        };
        _nudSize.ValueChanged += (_, _) => _brushSize = (int)_nudSize.Value;
        _toolTip.SetToolTip(_nudSize, "선 굵기 (1~50px)");
        _toolPanel.Controls.Add(_nudSize);
        curX += 56;

        // 구분선 2
        AddSeparator(ref curX);

        // 현재 색상 미리보기
        _pnlColorPreview = new Panel
        {
            Location = new Point(curX, 12),
            Size = new Size(26, 26),
            BackColor = _currentColor,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        _pnlColorPreview.Click += (_, _) => OpenAdvancedColorPicker();
        _toolTip.SetToolTip(_pnlColorPreview, "현재 선택된 색상\n(클릭하여 RGB/HEX/CMYK 팔레트 열기)");
        _toolPanel.Controls.Add(_pnlColorPreview);
        curX += 34;

        // 대표 프리셋 색상 칩들 (원형 칩)
        Color[] presets = [
            Color.FromArgb(15, 23, 42),   // 블랙
            Color.FromArgb(239, 68, 68),  // 레드
            Color.FromArgb(37, 99, 235),  // 블루
            Color.FromArgb(16, 185, 129), // 그린
            Color.FromArgb(245, 158, 11), // 옐로
            Color.FromArgb(168, 85, 247)  // 퍼플
        ];
        string[] presetNames = ["검정", "빨강", "파랑", "초록", "주황", "보라"];

        for (int i = 0; i < presets.Length; i++)
        {
            var c = presets[i];
            var name = presetNames[i];
            var pnlChip = new Panel
            {
                Location = new Point(curX, 15),
                Size = new Size(20, 20),
                BackColor = c,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            pnlChip.Click += (s, e) =>
            {
                _currentColor = c;
                _pnlColorPreview.BackColor = _currentColor;
            };
            _toolTip.SetToolTip(pnlChip, $"{name} 색상");
            _toolPanel.Controls.Add(pnlChip);
            curX += 26;
        }

        // [🎨 팔레트/CMYK...] 고급 색상 선택 버튼
        var btnPalette = new Button
        {
            Text = "🎨 팔레트/CMYK",
            Location = new Point(curX, 11),
            Size = new Size(115, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 8.5f),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            Cursor = Cursors.Hand
        };
        btnPalette.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnPalette.Click += (_, _) => OpenAdvancedColorPicker();
        _toolTip.SetToolTip(btnPalette, "전문가용 색상 선택기\nRGB, HEX, 인쇄용 CMYK 색상 코드 직접 입력");
        _toolPanel.Controls.Add(btnPalette);
        curX += 122;

        // 구분선 3
        AddSeparator(ref curX);

        // 실행 취소 / 다시 실행 / 모두 지우기
        _btnUndo = CreateActionBtn("↩️", "실행 취소 (Ctrl+Z)", ref curX, () => Undo());
        _btnUndo.Enabled = false;

        _btnRedo = CreateActionBtn("↪️", "다시 실행 (Ctrl+Y)", ref curX, () => Redo());
        _btnRedo.Enabled = false;

        var btnClear = CreateActionBtn("🗑️", "캔버스 모두 지우기", ref curX, () => ClearCanvas());

        Controls.Add(_toolPanel);

        // 2. 하단 상태 바 (캔버스 크기 안내 및 [문서에 삽입], [취소] 버튼)
        _bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16, 6, 16, 6)
        };
        _bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        };

        _lblCanvasSize = new Label
        {
            Text = "캔버스 크기: 1100 × 620 px (창 크기를 조절하면 캔버스도 함께 조절됩니다)",
            Location = new Point(16, 15),
            AutoSize = true,
            Font = new Font("맑은 고딕", 9f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        _bottomPanel.Controls.Add(_lblCanvasSize);

        var btnInsert = new Button
        {
            Text = "✔ 문서에 삽입(&I)",
            Size = new Size(130, 34),
            Location = new Point(ClientSize.Width - 235, 7),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnInsert.FlatAppearance.BorderSize = 0;
        btnInsert.Click += (_, _) =>
        {
            ResultImage = TrimAndExportBitmap(_canvasBitmap);
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "취소",
            Size = new Size(80, 34),
            Location = new Point(ClientSize.Width - 95, 7),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9f),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (_, _) => Close();

        _bottomPanel.Controls.AddRange([btnInsert, btnCancel]);
        Controls.Add(_bottomPanel);

        // 3. 중앙 캔버스 PictureBox (팝업 창에 100% 꽉 채움)
        _canvasBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Cursor = Cursors.Cross
        };
        _canvasBox.MouseDown += Canvas_MouseDown;
        _canvasBox.MouseMove += Canvas_MouseMove;
        _canvasBox.MouseUp += Canvas_MouseUp;
        _canvasBox.Paint += Canvas_Paint;
        Controls.Add(_canvasBox);
        _canvasBox.BringToFront();

        // 캔버스 초기화 및 반응형 리사이즈 연동
        int initW = Math.Max(100, ClientSize.Width);
        int initH = Math.Max(100, ClientSize.Height - _toolPanel.Height - _bottomPanel.Height);
        InitCanvas(initW, initH);

        Resize += DrawingDialog_Resize;

        // 단축키 지원
        KeyPreview = true;
        KeyDown += DrawingDialog_KeyDown;
    }

    private void AddSeparator(ref int curX)
    {
        curX += 4;
        _toolPanel.Controls.Add(new Panel
        {
            Location = new Point(curX, 13),
            Size = new Size(1, 26),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        curX += 12;
    }

    private void CreateToolIconBtn(string icon, DrawTool tool, string tooltip, ref int curX, bool isDefault = false)
    {
        var btn = new Button
        {
            Text = icon,
            Location = new Point(curX, 9),
            Size = new Size(38, 32),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 12f),
            Cursor = Cursors.Hand,
            BackColor = isDefault ? Color.FromArgb(224, 231, 255) : Color.White,
            ForeColor = isDefault ? Color.FromArgb(37, 99, 235) : Color.FromArgb(51, 65, 85)
        };
        btn.FlatAppearance.BorderColor = isDefault ? Color.FromArgb(59, 130, 246) : Color.FromArgb(226, 232, 240);
        btn.FlatAppearance.BorderSize = isDefault ? 2 : 1;

        btn.Click += (_, _) =>
        {
            _currentTool = tool;
            foreach (var (_, b) in _toolButtons)
            {
                b.BackColor = Color.White;
                b.ForeColor = Color.FromArgb(51, 65, 85);
                b.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
                b.FlatAppearance.BorderSize = 1;
            }
            btn.BackColor = Color.FromArgb(224, 231, 255);
            btn.ForeColor = Color.FromArgb(37, 99, 235);
            btn.FlatAppearance.BorderColor = Color.FromArgb(59, 130, 246);
            btn.FlatAppearance.BorderSize = 2;
        };

        _toolTip.SetToolTip(btn, tooltip);
        _toolPanel.Controls.Add(btn);
        _toolButtons.Add((tool, btn));
        curX += 42;
    }

    private Button CreateActionBtn(string icon, string tooltip, ref int curX, Action onClick)
    {
        var btn = new Button
        {
            Text = icon,
            Location = new Point(curX, 9),
            Size = new Size(38, 32),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 11f),
            Cursor = Cursors.Hand,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85)
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        btn.Click += (_, _) => onClick();
        _toolTip.SetToolTip(btn, tooltip);
        _toolPanel.Controls.Add(btn);
        curX += 42;
        return btn;
    }

    private void DrawingDialog_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) return;
        if (_canvasBox.Width <= 0 || _canvasBox.Height <= 0) return;

        ResizeCanvas(_canvasBox.Width, _canvasBox.Height);
    }

    private void InitCanvas(int width, int height)
    {
        width = Math.Max(100, width);
        height = Math.Max(100, height);

        _canvasBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.Clear(Color.White);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Image = _canvasBitmap;
        UpdateCanvasSizeLabel();
    }

    private void ResizeCanvas(int newW, int newH)
    {
        if (newW <= 0 || newH <= 0) return;
        if (_canvasBitmap != null && _canvasBitmap.Width == newW && _canvasBitmap.Height == newH) return;

        var newBmp = new Bitmap(newW, newH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(newBmp))
        {
            g.Clear(Color.White);
            if (_canvasBitmap != null)
            {
                g.DrawImageUnscaled(_canvasBitmap, 0, 0);
            }
        }

        _canvasGraphics?.Dispose();
        _canvasBitmap?.Dispose();

        _canvasBitmap = newBmp;
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Image = _canvasBitmap;
        _canvasBox.Invalidate();
        UpdateCanvasSizeLabel();
    }

    private void UpdateCanvasSizeLabel()
    {
        if (_lblCanvasSize != null && _canvasBitmap != null)
        {
            _lblCanvasSize.Text = $"캔버스 크기: {_canvasBitmap.Width} × {_canvasBitmap.Height} px (창 크기를 조절하면 캔버스도 함께 조절됩니다)";
        }
    }

    private void OpenAdvancedColorPicker()
    {
        using var picker = new ColorPickerForm(_currentColor);
        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            _currentColor = picker.SelectedColor;
            _pnlColorPreview.BackColor = _currentColor;
        }
    }

    private void PushUndo()
    {
        if (_canvasBitmap == null) return;
        _undoStack.Push(new Bitmap(_canvasBitmap));
        _redoStack.Clear();
        UpdateUndoRedoState();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(new Bitmap(_canvasBitmap));

        var prev = _undoStack.Pop();
        _canvasGraphics.Dispose();
        _canvasBitmap.Dispose();

        _canvasBitmap = prev;
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Image = _canvasBitmap;
        _canvasBox.Invalidate();
        UpdateUndoRedoState();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(new Bitmap(_canvasBitmap));

        var next = _redoStack.Pop();
        _canvasGraphics.Dispose();
        _canvasBitmap.Dispose();

        _canvasBitmap = next;
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Image = _canvasBitmap;
        _canvasBox.Invalidate();
        UpdateUndoRedoState();
    }

    private void UpdateUndoRedoState()
    {
        _btnUndo.Enabled = _undoStack.Count > 0;
        _btnRedo.Enabled = _redoStack.Count > 0;
    }

    private void ClearCanvas()
    {
        if (MessageBox.Show("캔버스의 모든 내용을 지우시겠습니까?", "모두 지우기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            PushUndo();
            _canvasGraphics.Clear(Color.White);
            _canvasBox.Invalidate();
        }
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        PushUndo();
        _isDrawing = true;
        _startPoint = e.Location;
        _lastPoint = e.Location;

        if (_currentTool is DrawTool.Pen or DrawTool.Highlighter or DrawTool.Eraser)
        {
            DrawLineSegment(_lastPoint, e.Location);
            _canvasBox.Invalidate();
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;

        if (_currentTool is DrawTool.Pen or DrawTool.Highlighter or DrawTool.Eraser)
        {
            DrawLineSegment(_lastPoint, e.Location);
            _lastPoint = e.Location;
            _canvasBox.Invalidate();
        }
        else
        {
            // 직선, 사각형, 원 도형 실시간 미리보기
            _tempBitmap?.Dispose();
            _tempBitmap = new Bitmap(_canvasBitmap);
            using (var g = Graphics.FromImage(_tempBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawShape(g, _startPoint, e.Location);
            }
            _canvasBox.Image = _tempBitmap;
        }
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        if (_currentTool is DrawTool.Line or DrawTool.Rectangle or DrawTool.Ellipse)
        {
            _tempBitmap?.Dispose();
            _tempBitmap = null;
            DrawShape(_canvasGraphics, _startPoint, e.Location);
            _canvasBox.Image = _canvasBitmap;
            _canvasBox.Invalidate();
        }
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        // 실시간 미리보기는 Image 프로퍼티를 통해 렌더링됨
    }

    private void DrawLineSegment(Point p1, Point p2)
    {
        switch (_currentTool)
        {
            case DrawTool.Pen:
                using (var pen = new Pen(_currentColor, _brushSize) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    _canvasGraphics.DrawLine(pen, p1, p2);
                }
                break;

            case DrawTool.Highlighter:
                // 반투명 형광펜 (알파값 90 적용)
                var hlColor = Color.FromArgb(90, _currentColor.R, _currentColor.G, _currentColor.B);
                using (var pen = new Pen(hlColor, _brushSize * 3) { StartCap = LineCap.Square, EndCap = LineCap.Square })
                {
                    _canvasGraphics.DrawLine(pen, p1, p2);
                }
                break;

            case DrawTool.Eraser:
                using (var pen = new Pen(Color.White, _brushSize * 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    _canvasGraphics.DrawLine(pen, p1, p2);
                }
                break;
        }
    }

    private void DrawShape(Graphics g, Point p1, Point p2)
    {
        var rect = new Rectangle(
            Math.Min(p1.X, p2.X),
            Math.Min(p1.Y, p2.Y),
            Math.Abs(p1.X - p2.X),
            Math.Abs(p1.Y - p2.Y)
        );

        using var pen = new Pen(_currentColor, _brushSize) { LineJoin = LineJoin.Round };

        switch (_currentTool)
        {
            case DrawTool.Line:
                g.DrawLine(pen, p1, p2);
                break;

            case DrawTool.Rectangle:
                if (rect.Width > 0 && rect.Height > 0)
                    g.DrawRectangle(pen, rect);
                break;

            case DrawTool.Ellipse:
                if (rect.Width > 0 && rect.Height > 0)
                    g.DrawEllipse(pen, rect);
                break;
        }
    }

    private void DrawingDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            Redo();
            e.SuppressKeyPress = true;
        }
    }

    /// <summary>
    /// 그려진 내용의 여백을 스마트 트리밍하여 내보냅니다.
    /// 아무것도 그리지 않았으면 전체 캔버스를 그대로 반환합니다.
    /// </summary>
    private static Bitmap TrimAndExportBitmap(Bitmap source)
    {
        int minX = source.Width;
        int minY = source.Height;
        int maxX = -1;
        int maxY = -1;

        // 흰색이 아닌 픽셀 경계 찾기
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                if (c.R < 250 || c.G < 250 || c.B < 250)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX >= minX && maxY >= minY)
        {
            // 여백 16px 추가
            int pad = 16;
            minX = Math.Max(0, minX - pad);
            minY = Math.Max(0, minY - pad);
            maxX = Math.Min(source.Width - 1, maxX + pad);
            maxY = Math.Min(source.Height - 1, maxY + pad);

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            var cropped = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(cropped);
            g.Clear(Color.White);
            g.DrawImage(source, new Rectangle(0, 0, w, h), new Rectangle(minX, minY, w, h), GraphicsUnit.Pixel);
            return cropped;
        }

        return new Bitmap(source);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _toolTip.Dispose();
        _tempBitmap?.Dispose();
        _canvasGraphics?.Dispose();
        _canvasBitmap?.Dispose();

        while (_undoStack.Count > 0) _undoStack.Pop().Dispose();
        while (_redoStack.Count > 0) _redoStack.Pop().Dispose();

        base.OnFormClosing(e);
    }
}
