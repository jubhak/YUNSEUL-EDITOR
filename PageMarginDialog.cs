using System.Drawing.Drawing2D;
using System.Drawing.Printing;

namespace YunseulMemo;

/// <summary>
/// A4 용지 여백 설정 대화상자.
/// 상/하/좌/우 여백(mm 단위) 설정 및 실시간 용지 미리보기를 제공합니다.
/// </summary>
public sealed class PageMarginDialog : Form
{
    private readonly NumericUpDown _nudTop;
    private readonly NumericUpDown _nudBottom;
    private readonly NumericUpDown _nudLeft;
    private readonly NumericUpDown _nudRight;
    private readonly Panel _pnlPreview;

    /// <summary>1/100인치 단위의 인쇄 및 레이아웃 Margins.</summary>
    public Margins ResultMargins
    {
        get
        {
            int left = MmToHundredthInch((int)_nudLeft.Value);
            int right = MmToHundredthInch((int)_nudRight.Value);
            int top = MmToHundredthInch((int)_nudTop.Value);
            int bottom = MmToHundredthInch((int)_nudBottom.Value);
            return new Margins(left, right, top, bottom);
        }
    }

    public PageMarginDialog(Margins currentMargins)
    {
        Text = "용지 여백 설정 (A4)";
        ClientSize = new Size(490, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f);

        // 좌측: 여백 입력 그룹
        var grpMargins = new GroupBox
        {
            Text = "여백 설정 (단위: mm)",
            Location = new Point(16, 16),
            Size = new Size(240, 260),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        int topMm = HundredthInchToMm(currentMargins.Top);
        int bottomMm = HundredthInchToMm(currentMargins.Bottom);
        int leftMm = HundredthInchToMm(currentMargins.Left);
        int rightMm = HundredthInchToMm(currentMargins.Right);

        int curY = 32;
        var lblTop = new Label { Text = "위쪽 (Top):", Location = new Point(16, curY + 3), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudTop = new NumericUpDown { Location = new Point(125, curY), Width = 70, Minimum = 0, Maximum = 100, Value = Math.Clamp(topMm, 0, 100), Font = new Font("Segoe UI", 9f) };
        grpMargins.Controls.AddRange([lblTop, _nudTop]);

        curY += 40;
        var lblBottom = new Label { Text = "아래쪽 (Bottom):", Location = new Point(16, curY + 3), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudBottom = new NumericUpDown { Location = new Point(125, curY), Width = 70, Minimum = 0, Maximum = 100, Value = Math.Clamp(bottomMm, 0, 100), Font = new Font("Segoe UI", 9f) };
        grpMargins.Controls.AddRange([lblBottom, _nudBottom]);

        curY += 40;
        var lblLeft = new Label { Text = "왼쪽 (Left):", Location = new Point(16, curY + 3), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudLeft = new NumericUpDown { Location = new Point(125, curY), Width = 70, Minimum = 0, Maximum = 100, Value = Math.Clamp(leftMm, 0, 100), Font = new Font("Segoe UI", 9f) };
        grpMargins.Controls.AddRange([lblLeft, _nudLeft]);

        curY += 40;
        var lblRight = new Label { Text = "오른쪽 (Right):", Location = new Point(16, curY + 3), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudRight = new NumericUpDown { Location = new Point(125, curY), Width = 70, Minimum = 0, Maximum = 100, Value = Math.Clamp(rightMm, 0, 100), Font = new Font("Segoe UI", 9f) };
        grpMargins.Controls.AddRange([lblRight, _nudRight]);

        curY += 46;
        var btnPresetDefault = new Button { Text = "기본 (20mm)", Location = new Point(16, curY), Width = 95, Height = 26, Font = new Font("Segoe UI", 8.5f) };
        btnPresetDefault.Click += (_, _) => SetAllMargins(20, 20, 25, 25);

        var btnPresetNarrow = new Button { Text = "좁게 (12mm)", Location = new Point(120, curY), Width = 95, Height = 26, Font = new Font("Segoe UI", 8.5f) };
        btnPresetNarrow.Click += (_, _) => SetAllMargins(12, 12, 15, 15);

        grpMargins.Controls.AddRange([btnPresetDefault, btnPresetNarrow]);

        // 우측: 실시간 용지 미리보기
        var grpPreview = new GroupBox
        {
            Text = "용지 미리보기",
            Location = new Point(270, 16),
            Size = new Size(200, 260),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _pnlPreview = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        _pnlPreview.Paint += DrawPagePreview;
        grpPreview.Controls.Add(_pnlPreview);

        _nudTop.ValueChanged += (_, _) => _pnlPreview.Invalidate();
        _nudBottom.ValueChanged += (_, _) => _pnlPreview.Invalidate();
        _nudLeft.ValueChanged += (_, _) => _pnlPreview.Invalidate();
        _nudRight.ValueChanged += (_, _) => _pnlPreview.Invalidate();

        // 하단 버튼 바
        var okBtn = new Button
        {
            Text = "확인",
            DialogResult = DialogResult.OK,
            Location = new Point(275, 295),
            Width = 95,
            Height = 36,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        okBtn.FlatAppearance.BorderSize = 0;

        var cancelBtn = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            Location = new Point(378, 295),
            Width = 85,
            Height = 36,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.AddRange([grpMargins, grpPreview, okBtn, cancelBtn]);
    }

    private void SetAllMargins(int left, int right, int top, int bottom)
    {
        _nudLeft.Value = left;
        _nudRight.Value = right;
        _nudTop.Value = top;
        _nudBottom.Value = bottom;
        _pnlPreview.Invalidate();
    }

    private static int HundredthInchToMm(int val) => (int)Math.Round(val * 0.254);
    private static int MmToHundredthInch(int val) => (int)Math.Round(val / 0.254);

    private void DrawPagePreview(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // A4 비율 (1 : 1.414)
        int paperW = 125;
        int paperH = (int)Math.Round(paperW * 1.414);

        int paperX = (_pnlPreview.Width - paperW) / 2;
        int paperY = (_pnlPreview.Height - paperH) / 2;

        var paperRect = new Rectangle(paperX, paperY, paperW, paperH);

        // 종이 그림자
        using var shadowPen = new Pen(Color.FromArgb(50, 0, 0, 0), 1);
        g.DrawRectangle(shadowPen, paperX + 2, paperY + 2, paperW, paperH);

        // 종이 본체 (흰색)
        using var brWhite = new SolidBrush(Color.White);
        g.FillRectangle(brWhite, paperRect);

        using var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1);
        g.DrawRectangle(borderPen, paperRect);

        // 여백 사각형 계산 (A4 가로 210mm, 세로 297mm 기준)
        float scaleX = paperW / 210f;
        float scaleY = paperH / 297f;

        float lm = (float)_nudLeft.Value * scaleX;
        float rm = (float)_nudRight.Value * scaleX;
        float tm = (float)_nudTop.Value * scaleY;
        float bm = (float)_nudBottom.Value * scaleY;

        var contentRect = new RectangleF(
            paperX + lm,
            paperY + tm,
            Math.Max(10, paperW - lm - rm),
            Math.Max(10, paperH - tm - bm));

        // 여백 가이드 점선
        using var dotPen = new Pen(Color.FromArgb(56, 189, 248), 1) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(dotPen, contentRect.X, contentRect.Y, contentRect.Width, contentRect.Height);

        // 인쇄 가능 영역 연한 텍스트 라인 모의 표시
        using var textLinePen = new Pen(Color.FromArgb(203, 213, 225), 1);
        float lineY = contentRect.Y + 8;
        while (lineY < contentRect.Bottom - 8)
        {
            g.DrawLine(textLinePen, contentRect.X + 4, lineY, contentRect.Right - 4, lineY);
            lineY += 9;
        }
    }
}
