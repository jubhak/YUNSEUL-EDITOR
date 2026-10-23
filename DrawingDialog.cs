using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace YunseulMemo;

/// <summary>
/// 자유 그림 그리기 및 문서 삽입 대화상자.
/// 펜, 형광펜, 지우개, 직선, 사각형, 원 도형, 색상 및 굵기 조절, 실행 취소/다시 실행을 지원합니다.
/// </summary>
public sealed class DrawingDialog : Form
{
    public enum DrawTool
    {
        Pen,         // 자유선 펜
        Highlighter, // 형광펜
        Eraser,      // 지우개
        Line,        // 직선
        Rectangle,   // 사각형
        Ellipse      // 타원/원
    }

    private readonly PictureBox _canvasBox;
    private Bitmap _canvasBitmap = null!;
    private Graphics _canvasGraphics = null!;

    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();

    private DrawTool _currentTool = DrawTool.Pen;
    private Color _currentColor = Color.FromArgb(15, 23, 42); // 기본 진한 네이비/블랙
    private int _brushSize = 3;
    private bool _isDrawing = false;
    private Point _startPoint;
    private Point _lastPoint;
    private Bitmap? _tempBitmap;

    private readonly Button _btnUndo;
    private readonly Button _btnRedo;
    private readonly Panel _pnlColorPreview;
    private readonly NumericUpDown _nudSize;

    /// <summary>완성된 그림 비트맵 (확인 시 제공).</summary>
    public Image? ResultImage { get; private set; }

    public DrawingDialog()
    {
        Text = "그림 그리기 (스케치/서명)";
        ClientSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(680, 500);
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 9.5f);

        // 1. 상단 툴바 패널
        var toolPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 10, 8)
        };
        toolPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, toolPanel.Height - 1, toolPanel.Width, toolPanel.Height - 1);
        };

        // 도구 라디오 버튼들
        int curX = 10;
        var btnPen = CreateToolBtn("펜", DrawTool.Pen, ref curX, toolPanel, isDefault: true);
        var btnHighlighter = CreateToolBtn("형광펜", DrawTool.Highlighter, ref curX, toolPanel);
        var btnEraser = CreateToolBtn("지우개", DrawTool.Eraser, ref curX, toolPanel);
        var btnLine = CreateToolBtn("직선", DrawTool.Line, ref curX, toolPanel);
        var btnRect = CreateToolBtn("사각형", DrawTool.Rectangle, ref curX, toolPanel);
        var btnCircle = CreateToolBtn("원", DrawTool.Ellipse, ref curX, toolPanel);

        // 구분 패널
        curX += 6;
        var sep1 = new Panel { Location = new Point(curX, 12), Size = new Size(1, 28), BackColor = Color.FromArgb(203, 213, 225) };
        toolPanel.Controls.Add(sep1);
        curX += 10;

        // 굵기 조절
        var lblSize = new Label { Text = "굵기:", Location = new Point(curX, 16), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        toolPanel.Controls.Add(lblSize);
        curX += 38;

        _nudSize = new NumericUpDown
        {
            Location = new Point(curX, 13),
            Width = 50,
            Minimum = 1,
            Maximum = 50,
            Value = _brushSize,
            Font = new Font("Segoe UI", 9f)
        };
        _nudSize.ValueChanged += (_, _) => _brushSize = (int)_nudSize.Value;
        toolPanel.Controls.Add(_nudSize);
        curX += 60;

        // 색상 선택 & 프리셋
        var lblColor = new Label { Text = "색상:", Location = new Point(curX, 16), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        toolPanel.Controls.Add(lblColor);
        curX += 38;

        _pnlColorPreview = new Panel
        {
            Location = new Point(curX, 13),
            Size = new Size(26, 26),
            BackColor = _currentColor,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        _pnlColorPreview.Click += (_, _) => ChooseCustomColor();
        toolPanel.Controls.Add(_pnlColorPreview);
        curX += 32;

        // 기본 프리셋 색상 칩 (블랙, 레드, 블루, 그린, 옐로)
        Color[] presets = [
            Color.FromArgb(15, 23, 42),   // 블랙/슬레이트
            Color.FromArgb(239, 68, 68),  // 레드
            Color.FromArgb(37, 99, 235),  // 블루
            Color.FromArgb(16, 185, 129), // 그린
            Color.FromArgb(245, 158, 11)  // 옐로/오렌지
        ];
        foreach (var c in presets)
        {
            var pnlChip = new Panel
            {
                Location = new Point(curX, 16),
                Size = new Size(18, 18),
                BackColor = c,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            pnlChip.Click += (s, e) =>
            {
                _currentColor = c;
                _pnlColorPreview.BackColor = _currentColor;
            };
            toolPanel.Controls.Add(pnlChip);
            curX += 22;
        }

        // 구분선 2
        curX += 4;
        var sep2 = new Panel { Location = new Point(curX, 12), Size = new Size(1, 28), BackColor = Color.FromArgb(203, 213, 225) };
        toolPanel.Controls.Add(sep2);
        curX += 10;

        // Undo / Redo / Clear
        _btnUndo = new Button { Text = "실행 취소", Location = new Point(curX, 12), Width = 72, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnUndo.Click += (_, _) => Undo();
        toolPanel.Controls.Add(_btnUndo);
        curX += 76;

        _btnRedo = new Button { Text = "다시 실행", Location = new Point(curX, 12), Width = 72, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnRedo.Click += (_, _) => Redo();
        toolPanel.Controls.Add(_btnRedo);
        curX += 76;

        var btnClear = new Button { Text = "모두 지우기", Location = new Point(curX, 12), Width = 80, Height = 28, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat };
        btnClear.Click += (_, _) => ClearCanvas();
        toolPanel.Controls.Add(btnClear);

        // 2. 하단 버튼 바 (문서에 삽입 / 취소)
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = Color.White,
            Padding = new Padding(16, 10, 16, 12)
        };
        bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
        };

        var btnInsert = new Button
        {
            Text = "문서에 삽입(&I)",
            Size = new Size(125, 36),
            Location = new Point(ClientSize.Width - 235, 11),
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
            Location = new Point(ClientSize.Width - 100, 11),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (_, _) => Close();

        bottomPanel.Controls.AddRange([btnInsert, btnCancel]);

        // 3. 중앙 캔버스 뷰
        var canvasContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(226, 232, 240),
            Padding = new Padding(20)
        };

        int canvasW = 1000;
        int canvasH = 700;
        _canvasBitmap = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb);
        _canvasGraphics = Graphics.FromImage(_canvasBitmap);
        _canvasGraphics.Clear(Color.White);
        _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

        _canvasBox = new PictureBox
        {
            Size = new Size(canvasW, canvasH),
            Location = new Point(20, 20),
            BackColor = Color.White,
            Image = _canvasBitmap,
            Cursor = Cursors.Cross
        };
        _canvasBox.Paint += (s, e) =>
        {
            using var shadowPen = new Pen(Color.FromArgb(180, 203, 213, 225), 1);
            e.Graphics.DrawRectangle(shadowPen, 0, 0, _canvasBox.Width - 1, _canvasBox.Height - 1);
        };

        _canvasBox.MouseDown += Canvas_MouseDown;
        _canvasBox.MouseMove += Canvas_MouseMove;
        _canvasBox.MouseUp += Canvas_MouseUp;

        canvasContainer.Controls.Add(_canvasBox);

        Controls.Add(canvasContainer);
        Controls.Add(bottomPanel);
        Controls.Add(toolPanel);

        AcceptButton = btnInsert;
        CancelButton = btnCancel;
    }

    private Button CreateToolBtn(string text, DrawTool tool, ref int curX, Panel parent, bool isDefault = false)
    {
        var btn = new Button
        {
            Text = text,
            Tag = tool,
            Location = new Point(curX, 12),
            Size = new Size(58, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
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
        curX += 62;
        return btn;
    }

    private void ChooseCustomColor()
    {
        using var cd = new ColorDialog { Color = _currentColor };
        if (cd.ShowDialog(this) == DialogResult.OK)
        {
            _currentColor = cd.Color;
            _pnlColorPreview.BackColor = _currentColor;
        }
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
        _canvasBox.Image = _canvasBitmap;
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
        _canvasBox.Image = _canvasBitmap;
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
                // 실시간 도형 가이드 그리기
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
            using var pen = new Pen(Color.White, _brushSize * 3)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            _canvasGraphics.DrawLine(pen, p1, p2);
        }
        else if (_currentTool == DrawTool.Highlighter)
        {
            // 반투명 형광펜
            using var pen = new Pen(Color.FromArgb(80, _currentColor.R, _currentColor.G, _currentColor.B), _brushSize * 3)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            _canvasGraphics.DrawLine(pen, p1, p2);
        }
        else
        {
            // 일반 펜
            using var pen = new Pen(_currentColor, _brushSize)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
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

    /// <summary>
    /// 그린 영역(비어있지 않은 영역)을 감지하여 적절한 여백으로 자동 크롭(Trim) 후 반환합니다.
    /// 아무것도 안 그렸으면 기본 크기로 반환.
    /// </summary>
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
                // 흰색이 아닌 픽셀 탐색
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

        if (!hasDrawing)
        {
            // 아무것도 안 그린 경우 원본 복사 반환
            return new Bitmap(src);
        }

        int pad = 24;
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
