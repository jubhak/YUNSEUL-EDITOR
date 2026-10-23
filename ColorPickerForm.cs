namespace YunseulMemo;

/// <summary>
/// RGB, HEX 및 인쇄용 CMYK 색상 코드 입력을 지원하는 고급 색상 선택기.
/// </summary>
public sealed class ColorPickerForm : Form
{
    private readonly NumericUpDown _nudR;
    private readonly NumericUpDown _nudG;
    private readonly NumericUpDown _nudB;
    private readonly TextBox _txtHex;

    private readonly NumericUpDown _nudC;
    private readonly NumericUpDown _nudM;
    private readonly NumericUpDown _nudY;
    private readonly NumericUpDown _nudK;

    private readonly Panel _pnlPreview;
    private bool _updating = false;

    public Color SelectedColor { get; private set; }

    public ColorPickerForm(Color initialColor)
    {
        SelectedColor = initialColor;

        Text = "색상 선택 (RGB / HEX / CMYK)";
        ClientSize = new Size(460, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f);

        // 1. 좌측: 색상 미리보기 및 프리셋 팔레트
        var grpPalette = new GroupBox
        {
            Text = "팔레트 및 미리보기",
            Location = new Point(16, 16),
            Size = new Size(180, 260),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _pnlPreview = new Panel
        {
            Location = new Point(16, 28),
            Size = new Size(148, 48),
            BackColor = SelectedColor,
            BorderStyle = BorderStyle.FixedSingle
        };
        grpPalette.Controls.Add(_pnlPreview);

        // 프리셋 24색 그리드
        Color[] palette = [
            Color.Black, Color.FromArgb(64, 64, 64), Color.FromArgb(128, 128, 128), Color.White,
            Color.FromArgb(239, 68, 68), Color.FromArgb(249, 115, 22), Color.FromArgb(245, 158, 11), Color.FromArgb(234, 179, 8),
            Color.FromArgb(16, 185, 129), Color.FromArgb(20, 184, 166), Color.FromArgb(6, 182, 212), Color.FromArgb(14, 165, 233),
            Color.FromArgb(59, 130, 246), Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246), Color.FromArgb(168, 85, 247),
            Color.FromArgb(217, 70, 239), Color.FromArgb(244, 63, 94), Color.FromArgb(136, 19, 55), Color.FromArgb(30, 41, 59)
        ];

        int px = 16, py = 88;
        for (int i = 0; i < palette.Length; i++)
        {
            var col = palette[i];
            var chip = new Panel
            {
                Location = new Point(px, py),
                Size = new Size(22, 22),
                BackColor = col,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            chip.Click += (s, e) => UpdateFromColor(col);
            grpPalette.Controls.Add(chip);

            px += 26;
            if ((i + 1) % 5 == 0)
            {
                px = 16;
                py += 26;
            }
        }

        // 2. 우측 상단: RGB & HEX 입력
        var grpDigital = new GroupBox
        {
            Text = "디지털 색상 (RGB / HEX)",
            Location = new Point(206, 16),
            Size = new Size(238, 125),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var lblR = new Label { Text = "R:", Location = new Point(12, 28), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudR = new NumericUpDown { Location = new Point(32, 25), Width = 46, Minimum = 0, Maximum = 255, Value = SelectedColor.R, Font = new Font("Segoe UI", 9f) };

        var lblG = new Label { Text = "G:", Location = new Point(84, 28), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudG = new NumericUpDown { Location = new Point(106, 25), Width = 46, Minimum = 0, Maximum = 255, Value = SelectedColor.G, Font = new Font("Segoe UI", 9f) };

        var lblB = new Label { Text = "B:", Location = new Point(158, 28), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudB = new NumericUpDown { Location = new Point(178, 25), Width = 46, Minimum = 0, Maximum = 255, Value = SelectedColor.B, Font = new Font("Segoe UI", 9f) };

        var lblHex = new Label { Text = "HEX (#):", Location = new Point(12, 68), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _txtHex = new TextBox { Location = new Point(78, 65), Width = 146, Font = new Font("Segoe UI", 9.5f), Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}" };

        _nudR.ValueChanged += (_, _) => OnRgbChanged();
        _nudG.ValueChanged += (_, _) => OnRgbChanged();
        _nudB.ValueChanged += (_, _) => OnRgbChanged();
        _txtHex.TextChanged += (_, _) => OnHexChanged();

        grpDigital.Controls.AddRange([lblR, _nudR, lblG, _nudG, lblB, _nudB, lblHex, _txtHex]);

        // 3. 우측 하단: 인쇄용 CMYK 입력
        var grpCmyk = new GroupBox
        {
            Text = "인쇄용 색상 (CMYK %)",
            Location = new Point(206, 148),
            Size = new Size(238, 128),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var lblC = new Label { Text = "C:", Location = new Point(14, 30), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudC = new NumericUpDown { Location = new Point(34, 27), Width = 55, Minimum = 0, Maximum = 100, Font = new Font("Segoe UI", 9f) };

        var lblM = new Label { Text = "M:", Location = new Point(120, 30), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudM = new NumericUpDown { Location = new Point(144, 27), Width = 55, Minimum = 0, Maximum = 100, Font = new Font("Segoe UI", 9f) };

        var lblY = new Label { Text = "Y:", Location = new Point(14, 72), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudY = new NumericUpDown { Location = new Point(34, 69), Width = 55, Minimum = 0, Maximum = 100, Font = new Font("Segoe UI", 9f) };

        var lblK = new Label { Text = "K:", Location = new Point(120, 72), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        _nudK = new NumericUpDown { Location = new Point(144, 69), Width = 55, Minimum = 0, Maximum = 100, Font = new Font("Segoe UI", 9f) };

        _nudC.ValueChanged += (_, _) => OnCmykChanged();
        _nudM.ValueChanged += (_, _) => OnCmykChanged();
        _nudY.ValueChanged += (_, _) => OnCmykChanged();
        _nudK.ValueChanged += (_, _) => OnCmykChanged();

        grpCmyk.Controls.AddRange([lblC, _nudC, lblM, _nudM, lblY, _nudY, lblK, _nudK]);

        // 하단 OK / Cancel 버튼
        var okBtn = new Button
        {
            Text = "선택",
            DialogResult = DialogResult.OK,
            Location = new Point(245, 295),
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
            Location = new Point(348, 295),
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

        Controls.AddRange([grpPalette, grpDigital, grpCmyk, okBtn, cancelBtn]);

        SyncCmykFromRgb(SelectedColor);
    }

    private void UpdateFromColor(Color c)
    {
        if (_updating) return;
        _updating = true;
        SelectedColor = c;
        _pnlPreview.BackColor = c;
        _nudR.Value = c.R;
        _nudG.Value = c.G;
        _nudB.Value = c.B;
        _txtHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        SyncCmykFromRgb(c);
        _updating = false;
    }

    private void OnRgbChanged()
    {
        if (_updating) return;
        _updating = true;
        var c = Color.FromArgb((int)_nudR.Value, (int)_nudG.Value, (int)_nudB.Value);
        SelectedColor = c;
        _pnlPreview.BackColor = c;
        _txtHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        SyncCmykFromRgb(c);
        _updating = false;
    }

    private void OnHexChanged()
    {
        if (_updating) return;
        string hex = _txtHex.Text.Trim().TrimStart('#');
        if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int val))
        {
            _updating = true;
            int r = (val >> 16) & 0xFF;
            int g = (val >> 8) & 0xFF;
            int b = val & 0xFF;
            var c = Color.FromArgb(r, g, b);
            SelectedColor = c;
            _pnlPreview.BackColor = c;
            _nudR.Value = r;
            _nudG.Value = g;
            _nudB.Value = b;
            SyncCmykFromRgb(c);
            _updating = false;
        }
    }

    private void SyncCmykFromRgb(Color c)
    {
        float r = c.R / 255f;
        float g = c.G / 255f;
        float b = c.B / 255f;

        float k = 1f - Math.Max(r, Math.Max(g, b));
        float cyan = (k < 1f) ? (1f - r - k) / (1f - k) : 0f;
        float magenta = (k < 1f) ? (1f - g - k) / (1f - k) : 0f;
        float yellow = (k < 1f) ? (1f - b - k) / (1f - k) : 0f;

        _nudC.Value = (decimal)Math.Round(cyan * 100);
        _nudM.Value = (decimal)Math.Round(magenta * 100);
        _nudY.Value = (decimal)Math.Round(yellow * 100);
        _nudK.Value = (decimal)Math.Round(k * 100);
    }

    private void OnCmykChanged()
    {
        if (_updating) return;
        _updating = true;

        float c = (float)_nudC.Value / 100f;
        float m = (float)_nudM.Value / 100f;
        float y = (float)_nudY.Value / 100f;
        float k = (float)_nudK.Value / 100f;

        int r = (int)Math.Clamp(Math.Round(255f * (1f - c) * (1f - k)), 0, 255);
        int g = (int)Math.Clamp(Math.Round(255f * (1f - m) * (1f - k)), 0, 255);
        int b = (int)Math.Clamp(Math.Round(255f * (1f - y) * (1f - k)), 0, 255);

        var col = Color.FromArgb(r, g, b);
        SelectedColor = col;
        _pnlPreview.BackColor = col;
        _nudR.Value = r;
        _nudG.Value = g;
        _nudB.Value = b;
        _txtHex.Text = $"#{r:X2}{g:X2}{b:X2}";

        _updating = false;
    }
}
