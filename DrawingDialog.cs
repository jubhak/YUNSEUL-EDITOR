using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace YunseulMemo;

/// <summary>
/// 고급 그림 그리기 (스케치/서명/도형) 대화상자.
/// 캔버스 우측 하단 드래그 크기 조절, RGB/HEX/CMYK 색상 입력, 펜/형광펜/지우개/도형, 실행 취소/다시 실행을 지원합니다.
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
    private readonly Panel _canvasContainer;
    private readonly PictureBox _canvasBox;
    private readonly Panel _resizeHandle;
    private readonly Label _lblCanvasSize;

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

    // 캔버스 리사이즈 드래그 상태
    private bool _isResizingCanvas = false;
    private Point _resizeStartMouse;
    private Size _resizeStartSize;

    private readonly Button _btnUndo;
    private readonly Button _btnRedo;
    private readonly Panel _pnlColorPreview;
    private readonly NumericUpDown _nudSize;

    /// <summary>완성된 그림 비트맵.</summary>
    public Image? ResultImage { get; private set; }

    public DrawingDialog()
    {
        Text = "그림 그리기 (스케치 / 서명 / 도형)";
        ClientSize = new Size(1060, 720); // 메뉴가 전혀 잘리지 않는 넉넉한 기본 폭
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(920, 600);
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 9.5f);

        // 1. 상단 툴바 패널
        _toolPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = Color.White,
            Padding = new Padding(12, 10, 12, 10)
        };
        _toolPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, _toolPanel.Height - 1, _toolPanel.Width, _toolPanel.Height - 1);
        };

        int curX = 12;
        CreateToolBtn("자유펜", DrawTool.Pen, ref curX, _toolPanel, isDefault: true);
        CreateToolBtn("형광펜", DrawTool.Highlighter, ref curX, _toolPanel);
        CreateToolBtn("지우개", DrawTool.Eraser, ref curX, _toolPanel);
        CreateToolBtn("직선", DrawTool.Line, ref curX, _toolPanel);
        CreateToolBtn("사각형", DrawTool.Rectangle, ref curX, _toolPanel);
        CreateToolBtn("원/타원", DrawTool.Ellipse, ref curX, _toolPanel);

        // 구분선 1
        curX += 4;
        _toolPanel.Controls.Add(new Panel { Location = new Point(curX, 14), Size = new Size(1, 30), BackColor = Color.FromArgb(203, 213, 225) });
        curX += 12;

        // 굵기
        var lblSize = new Label { Text = "굵기:", Location = new Point(curX, 19), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _toolPanel.Controls.Add(lblSize);
        curX += 36;

        _nudSize = new NumericUpDown
        {
            Location = new Point(curX, 15),
            Width = 52,
            Minimum = 1,
            Maximum = 50,
            Value = _brushSize,
            Font = new Font("Segoe UI", 9f)
        };
        _nudSize.ValueChanged += (_, _) => _brushSize = (int)_nudSize.Value;
        _toolPanel.Controls.Add(_nudSize);
        curX += 62;

        // 색상 미리보기
        _pnlColorPreview = new Panel
        {
            Location = new Point(curX, 14),
            Size = new Size(28, 28),
            BackColor = _currentColor,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        _pnlColorPreview.Click += (_, _) => OpenAdvancedColorPicker();
        _toolPanel.Controls.Add(_pnlColorPreview);
        curX += 34;

        // 대표 프리셋 색상들
        Color[] presets = [
            Color.FromArgb(15, 23, 42),   // 블랙
            Color.FromArgb(239, 68, 68),  // 레드
            Color.FromArgb(37, 99, 235),  // 블루
            Color.FromArgb(16, 185, 129), // 그린
            Color.FromArgb(245, 158, 11), // 옐로
            Color.FromArgb(168, 85, 247)  // 퍼플
        ];
        foreach (var c in presets)
        {
            var pnlChip = new Panel
            {
                Location = new Point(curX, 18),
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
            _toolPanel.Controls.Add(pnlChip);
            curX += 24;
        }

        // [팔레트 / CMYK...] 고급 색상 선택 버튼
        var btnPalette = new Button
        {
            Text = "팔레트/CMYK...",
            Location = new Point(curX, 14),
            Size = new Size(105, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnPalette.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnPalette.Click += (_, _) => OpenAdvancedColorPicker();
        _toolPanel.Controls.Add(btnPalette);
        curX += 112;

        // 구분선 2
        _toolPanel.Controls.Add(new Panel { Location = new Point(curX, 14), Size = new Size(1, 30), BackColor = Color.FromArgb(203, 213, 225) });
        curX += 12;

        // 실행 취소 / 다시 실행 / 모두 지우기
        _btnUndo = new Button { Text = "실행 취소", Location = new Point(curX, 14), Width = 75, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnUndo.Click += (_, _) => Undo();
        _toolPanel.Controls.Add(_btnUndo);
        curX += 80;

        _btnRedo = new Button { Text = "다시 실행", Location = new Point(curX, 14), Width = 75, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnRedo.Click += (_, _) => Redo();
        _toolPanel.Controls.Add(_btnRedo);
        curX += 80;

        var btnClear = new Button { Text = "모두 지우기", Location = new Point(curX, 14), Width = 85, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat };
        btnClear.Click += (_, _) => ClearCanvas();
        _toolPanel.Controls.Add(btnClear);

        // 2. 하단 버튼 바 (크기 상태 표시 + 문서 삽입 / 취소)
        _bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = Color.White,
            Padding = new Padding(16, 8, 16, 10)
        };
        _bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        };

        _lblCanvasSize = new Label
        {
            Text = "캔버스 크기: 800 × 500 px (우측 하단 모서리를 끌어서 조절)",
            Location = new Point(16, 16),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        _bottomPanel.Controls.Add(_lblCanvasSize);

        var btnInsert = new Button
        {
            Text = "문서에 삽입(&I)",
            Size = new Size(130, 36),
            Location = new Point(ClientSize.Width - 240, 9),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
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
            Size = new Size(85, 36),
            Location = new Point(ClientSize.Width - 100, 9),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (_, _) => Close();

        _bottomPanel.Controls.AddRange([btnInsert, btnCancel]);

        // 3. 중앙 캔버스 뷰 (하단 여백 버그 해결을 위해 Fill 도킹)
        _canvasContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(226, 232, 240),
            Padding = new Padding(20)
        };

        int initialW = 800;
        int initialH = 500;
        InitCanvasBitmap(initialW, initialH);

        _canvasBox = new PictureBox
        {
            Location = new Point(20, 20),
            Size = new Size(initialW, initialH),
            BackColor = Color.White,
            Image = _canvasBitmap,
            Cursor = Cursors.Cross
        };
        _canvasBox.Paint += (s, e) =>
        {
            using var shadowPen = new Pen(Color.FromArgb(160, 180, 200), 1);
            e.Graphics.DrawRectangle(shadowPen, 0, 0, _canvasBox.Width - 1, _canvasBox.Height - 1);
        };

        _canvasBox.MouseDown += Canvas_MouseDown;
        _canvasBox.MouseMove += Canvas_MouseMove;
        _canvasBox.MouseUp += Canvas_MouseUp;

        // 4. 캔버스 우측 하단 리사이즈 핸들 아이콘
        _resizeHandle = new Panel
        {
            Size = new Size(14, 14),
            BackColor = Color.FromArgb(59, 130, 246),
            Cursor = Cursors.SizeNWSE
        };
        _resizeHandle.Paint += (s, e) =>
        {
            // 우측 하단 사선 그립 패턴 그리기
            using var pen = new Pen(Color.White, 1.2f);
            e.Graphics.DrawLine(pen, 4, 12, 12, 4);
            e.Graphics.DrawLine(pen, 8, 12, 12, 8);
        };
        _resizeHandle.MouseDown += ResizeHandle_MouseDown;
        _resizeHandle.MouseMove += ResizeHandle_MouseMove;
        _resizeHandle.MouseUp += ResizeHandle_MouseUp;

        _canvasContainer.Controls.Add(_resizeHandle);
        _canvasContainer.Controls.Add(_canvasBox);

        // 도킹 순서: Bottom 먼저, Top 그 다음, Fill 마지막 추가
        Controls.Add(_canvasContainer);
        Controls.Add(_bottomPanel);
        Controls.Add(_toolPanel);

        UpdateResizeHandlePosition();
        UpdateSizeLabel();

        AcceptButton = btnInsert;
        CancelButton = btnCancel;
    }

    private void InitCanvasBitmap(int w, int h)
    {
        _canvasBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.Clear(Color.White);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;
    }

    private void UpdateResizeHandlePosition()
    {
        // 캔버스 박스의 우측 하단 꼭짓점에 핸들 배치
        _resizeHandle.Location = new Point(_canvasBox.Right - 4, _canvasBox.Bottom - 4);
        _resizeHandle.BringToFront();
    }

    private void UpdateSizeLabel()
    {
        _lblCanvasSize.Text = $"캔버스 크기: {_canvasBox.Width} × {_canvasBox.Height} px (우측 하단 모서리를 끌어서 조절)";
    }

    private void ResizeHandle_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isResizingCanvas = true;
            _resizeStartMouse = Cursor.Position;
            _resizeStartSize = _canvasBox.Size;
            SaveStateForUndo();
        }
    }

    private void ResizeHandle_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isResizingCanvas) return;

        var currentMouse = Cursor.Position;
        int diffX = currentMouse.X - _resizeStartMouse.X;
        int diffY = currentMouse.Y - _resizeStartMouse.Y;

        int newW = Math.Clamp(_resizeStartSize.Width + diffX, 200, 2400);
        int newH = Math.Clamp(_resizeStartSize.Height + diffY, 150, 1800);

        if (newW != _canvasBox.Width || newH != _canvasBox.Height)
        {
            // 새 크기로 비트맵 확장/축소 (기존 그림 유지)
            var newBmp = new Bitmap(newW, newH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(newBmp))
            {
                g.Clear(Color.White);
                g.DrawImage(_canvasBitmap, 0, 0);
            }

            _canvasBitmap.Dispose();
            _canvasGraphics.Dispose();

            _canvasBitmap = newBmp;
            _canvasGraphics = Graphics.FromImage(_canvasBitmap);
            _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

            _canvasBox.Size = new Size(newW, newH);
            _canvasBox.Image = _canvasBitmap;
            UpdateResizeHandlePosition();
            UpdateSizeLabel();
        }
    }

    private void ResizeHandle_MouseUp(object? sender, MouseEventArgs e)
    {
        _isResizingCanvas = false;
    }

    private void OpenAdvancedColorPicker()
    {
        using var dlg = new ColorPickerForm(_currentColor);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _currentColor = dlg.SelectedColor;
            _pnlColorPreview.BackColor = _currentColor;
        }
    }

    private Button CreateToolBtn(string text, DrawTool tool, ref int curX, Panel parent, bool isDefault = false)
    {
        var btn = new Button
        {
            Text = text,
            Tag = tool,
            Location = new Point(curX, 13),
            Size = new Size(68, 30), // 글씨가 절대 잘리지 않도록 68px 지정
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            BackColor = isDefault ? Color.FromArgb(219, 234, 254) : Color.White,
            ForeColor = isDefault ? Color.FromArgb(29, 78, 216) : Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = isDefault ? Color.FromArgb(59, 130, 246) : Color.FromArgb(203, 213, 225);

        btn.Click += (s, e) =>
        {
            _currentTool = tool;
            foreach (Control c in parent.Controls)
            {
                if (c is Button b && b.Tag is DrawTool)
                {
                    bool isCurrent = (b == btn);
                    b.BackColor = isCurrent ? Color.FromArgb(219, 234, 254) : Color.White;
                    b.ForeColor = isCurrent ? Color.FromArgb(29, 78, 216) : Color.FromArgb(30, 41, 59);
                    b.FlatAppearance.BorderColor = isCurrent ? Color.FromArgb(59, 130, 246) : Color.FromArgb(203, 213, 225);
                }
            }
        };

        parent.Controls.Add(btn);
        curX += 72;
        return btn;
    }

    private void SaveStateForUndo()
    {
        _undoStack.Push(new Bitmap(_canvasBitmap));
        _redoStack.Clear();
        _btnUndo.Enabled = true;
        _btnRedo.Enabled = false;
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(new Bitmap(_canvasBitmap));
        var prev = _undoStack.Pop();
        _canvasBitmap.Dispose();
        _canvasGraphics.Dispose();

        _canvasBitmap = prev;
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Size = _canvasBitmap.Size;
        _canvasBox.Image = _canvasBitmap;
        UpdateResizeHandlePosition();
        UpdateSizeLabel();
        _canvasBox.Invalidate();

        _btnUndo.Enabled = _undoStack.Count > 0;
        _btnRedo.Enabled = true;
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(new Bitmap(_canvasBitmap));
        var next = _redoStack.Pop();
        _canvasBitmap.Dispose();
        _canvasGraphics.Dispose();

        _canvasBitmap = next;
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox.Size = _canvasBitmap.Size;
        _canvasBox.Image = _canvasBitmap;
        UpdateResizeHandlePosition();
        UpdateSizeLabel();
        _canvasBox.Invalidate();

        _btnUndo.Enabled = true;
        _btnRedo.Enabled = _redoStack.Count > 0;
    }

    private void ClearCanvas()
    {
        SaveStateForUndo();
        _canvasGraphics.Clear(Color.White);
        _canvasBox.Invalidate();
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        SaveStateForUndo();

        _isDrawing = true;
        _startPoint = e.Location;
        _lastPoint = e.Location;

        if (_currentTool is DrawTool.Line or DrawTool.Rectangle or DrawTool.Ellipse)
        {
            _tempBitmap = new Bitmap(_canvasBitmap);
        }
        else
        {
            DrawStroke(_lastPoint, e.Location);
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;

        if (_currentTool is DrawTool.Line or DrawTool.Rectangle or DrawTool.Ellipse)
        {
            if (_tempBitmap != null)
            {
                _canvasGraphics.DrawImage(_tempBitmap, 0, 0);
                DrawShape(_startPoint, e.Location, _canvasGraphics);
                _canvasBox.Invalidate();
            }
        }
        else
        {
            DrawStroke(_lastPoint, e.Location);
            _lastPoint = e.Location;
            _canvasBox.Invalidate();
        }
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        if (_currentTool is DrawTool.Line or DrawTool.Rectangle or DrawTool.Ellipse)
        {
            if (_tempBitmap != null)
            {
                _canvasGraphics.DrawImage(_tempBitmap, 0, 0);
                DrawShape(_startPoint, e.Location, _canvasGraphics);
                _tempBitmap.Dispose();
                _tempBitmap = null;
                _canvasBox.Invalidate();
            }
        }
    }

    private void DrawStroke(Point p1, Point p2)
    {
        if (_currentTool == DrawTool.Eraser)
        {
            using var pen = new Pen(Color.White, _brushSize * 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            _canvasGraphics.DrawLine(pen, p1, p2);
        }
        else if (_currentTool == DrawTool.Highlighter)
        {
            using var pen = new Pen(Color.FromArgb(80, _currentColor.R, _currentColor.G, _currentColor.B), _brushSize * 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            _canvasGraphics.DrawLine(pen, p1, p2);
        }
        else
        {
            using var pen = new Pen(_currentColor, _brushSize) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            _canvasGraphics.DrawLine(pen, p1, p2);
        }
    }

    private void DrawShape(Point p1, Point p2, Graphics g)
    {
        using var pen = new Pen(_currentColor, _brushSize);
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p1.X - p2.X);
        int h = Math.Abs(p1.Y - p2.Y);

        switch (_currentTool)
        {
            case DrawTool.Line:
                g.DrawLine(pen, p1, p2);
                break;
            case DrawTool.Rectangle:
                g.DrawRectangle(pen, x, y, w, h);
                break;
            case DrawTool.Ellipse:
                g.DrawEllipse(pen, x, y, w, h);
                break;
        }
    }

    private static Bitmap TrimAndExportBitmap(Bitmap src)
    {
        int minX = src.Width, maxX = 0;
        int minY = src.Height, maxY = 0;
        bool hasDrawing = false;

        for (int y = 0; y < src.Height; y += 2)
        {
            for (int x = 0; x < src.Width; x += 2)
            {
                var c = src.GetPixel(x, y);
                if (c.R < 250 || c.G < 250 || c.B < 250)
                {
                    hasDrawing = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (!hasDrawing) return new Bitmap(src);

        int pad = 20;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(src.Width - 1, maxX + pad);
        maxY = Math.Min(src.Height - 1, maxY + pad);

        int cropW = Math.Max(32, maxX - minX + 1);
        int cropH = Math.Max(32, maxY - minY + 1);

        var cropped = new Bitmap(cropW, cropH);
        using (var g = Graphics.FromImage(cropped))
        {
            g.Clear(Color.White);
            g.DrawImage(src, new Rectangle(0, 0, cropW, cropH), new Rectangle(minX, minY, cropW, cropH), GraphicsUnit.Pixel);
        }
        return cropped;
    }
}
