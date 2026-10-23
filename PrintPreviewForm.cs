using System.Drawing.Printing;

namespace YunseulMemo;

/// <summary>
/// 인쇄 미리보기 폼. 여백 조정, 머리말/꼬리말 설정 및 A4 출력 최적화 포함.
/// </summary>
public class PrintPreviewForm : Form
{
    private readonly PrintDocument _printDoc;
    private PrintPreviewControl _previewControl = null!;
    private NumericUpDown _marginTop = null!;
    private NumericUpDown _marginBottom = null!;
    private NumericUpDown _marginLeft = null!;
    private NumericUpDown _marginRight = null!;
    private TextBox _txtHeader = null!;
    private TextBox _txtFooter = null!;
    private readonly RichTextBox _editor;
    private int _charIndex;
    private int _pageNumber = 1;

    public Margins CurrentMargins => _printDoc.DefaultPageSettings.Margins;

    public PrintPreviewForm(RichTextBox editor, Margins? initialMargins = null)
    {
        _editor = editor;
        _printDoc = new PrintDocument();

        // A4 용지 기본 설정
        try
        {
            foreach (PaperSize ps in _printDoc.PrinterSettings.PaperSizes)
            {
                if (ps.Kind == PaperKind.A4)
                {
                    _printDoc.DefaultPageSettings.PaperSize = ps;
                    break;
                }
            }
        }
        catch { }

        _printDoc.BeginPrint += (s, e) =>
        {
            _charIndex = 0;
            _pageNumber = 1;
        };
        _printDoc.PrintPage += PrintDoc_PrintPage;

        Text = "인쇄 미리보기 (A4 용지 기준)";
        Size = new Size(980, 780);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        BuildUI(initialMargins);
        UpdatePreview();
    }

    private void BuildUI(Margins? init)
    {
        // Top panel with margin, header, footer controls and buttons
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("맑은 고딕", 9f)
        };

        var borderLine = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        topPanel.Controls.Add(borderLine);

        int topVal = init?.Top ?? 98;
        int botVal = init?.Bottom ?? 98;
        int leftVal = init?.Left ?? 79;
        int rightVal = init?.Right ?? 79;

        // Row 1: 여백 설정 및 동작 버튼
        int y1 = 10;
        int x = 12;

        var lblMargin = new Label { Text = "여백(px):", Location = new Point(x, y1 + 3), AutoSize = true, Font = new Font("맑은 고딕", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        topPanel.Controls.Add(lblMargin);
        x += 65;

        topPanel.Controls.Add(new Label { Text = "위:", Location = new Point(x, y1 + 3), AutoSize = true });
        x += 25;
        _marginTop = new NumericUpDown { Location = new Point(x, y1), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(topVal, 0, 300) };
        _marginTop.ValueChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_marginTop);
        x += 62;

        topPanel.Controls.Add(new Label { Text = "아래:", Location = new Point(x, y1 + 3), AutoSize = true });
        x += 35;
        _marginBottom = new NumericUpDown { Location = new Point(x, y1), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(botVal, 0, 300) };
        _marginBottom.ValueChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_marginBottom);
        x += 62;

        topPanel.Controls.Add(new Label { Text = "왼쪽:", Location = new Point(x, y1 + 3), AutoSize = true });
        x += 35;
        _marginLeft = new NumericUpDown { Location = new Point(x, y1), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(leftVal, 0, 300) };
        _marginLeft.ValueChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_marginLeft);
        x += 62;

        topPanel.Controls.Add(new Label { Text = "오른쪽:", Location = new Point(x, y1 + 3), AutoSize = true });
        x += 48;
        _marginRight = new NumericUpDown { Location = new Point(x, y1), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(rightVal, 0, 300) };
        _marginRight.ValueChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_marginRight);
        x += 75;

        var printBtn = new Button
        {
            Text = "🖨 인쇄하기",
            Location = new Point(x, y1 - 2),
            Width = 95,
            Height = 30,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9f, FontStyle.Bold)
        };
        printBtn.FlatAppearance.BorderSize = 0;
        printBtn.Click += PrintBtn_Click;
        topPanel.Controls.Add(printBtn);
        x += 105;

        var closeBtn = new Button
        {
            Text = "닫기",
            Location = new Point(x, y1 - 2),
            Width = 70,
            Height = 30,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        closeBtn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        closeBtn.Click += (s, e) => Close();
        topPanel.Controls.Add(closeBtn);

        // Row 2: 머리말 / 꼬리말 설정
        int y2 = 45;
        x = 12;

        var lblHeader = new Label { Text = "머리말:", Location = new Point(x, y2 + 3), AutoSize = true, Font = new Font("맑은 고딕", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        topPanel.Controls.Add(lblHeader);
        x += 55;

        _txtHeader = new TextBox { Location = new Point(x, y2), Width = 220, PlaceholderText = "상단 머리말 (비워두면 없음)" };
        _txtHeader.TextChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_txtHeader);
        x += 235;

        var lblFooter = new Label { Text = "꼬리말:", Location = new Point(x, y2 + 3), AutoSize = true, Font = new Font("맑은 고딕", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        topPanel.Controls.Add(lblFooter);
        x += 55;

        _txtFooter = new TextBox { Location = new Point(x, y2), Width = 220, Text = "- [페이지] -", PlaceholderText = "- [페이지] -" };
        _txtFooter.TextChanged += (s, e) => UpdatePreview();
        topPanel.Controls.Add(_txtFooter);
        x += 230;

        var tipLabel = new Label
        {
            Text = "(꼬리말 '[페이지]'는 번호로 자동 치환)",
            Location = new Point(x, y2 + 4),
            AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("맑은 고딕", 8.25f)
        };
        topPanel.Controls.Add(tipLabel);

        Controls.Add(topPanel);

        // Preview control
        _previewControl = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Document = _printDoc,
            Zoom = 0.85,
            AutoZoom = false,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        Controls.Add(_previewControl);
        _previewControl.BringToFront();
    }

    private void UpdatePreview()
    {
        _printDoc.DefaultPageSettings.Margins = new Margins(
            (int)_marginLeft.Value,
            (int)_marginRight.Value,
            (int)_marginTop.Value,
            (int)_marginBottom.Value
        );
        _charIndex = 0;
        _pageNumber = 1;
        _previewControl.InvalidatePreview();
    }

    private void PrintDoc_PrintPage(object? sender, PrintPageEventArgs e)
    {
        RtfPrintHelper.PrintPage(
            _editor,
            e,
            ref _charIndex,
            _pageNumber++,
            _txtHeader?.Text,
            _txtFooter?.Text
        );
    }

    private void PrintBtn_Click(object? sender, EventArgs e)
    {
        using var printDlg = new PrintDialog { Document = _printDoc, UseEXDialog = true };
        if (printDlg.ShowDialog(this) == DialogResult.OK)
        {
            _charIndex = 0;
            _pageNumber = 1;
            _printDoc.Print();
            Close();
        }
    }
}
