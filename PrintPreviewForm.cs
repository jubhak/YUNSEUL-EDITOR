using System.Drawing.Printing;

namespace YunseulMemo;

/// <summary>
/// 인쇄 미리보기 폼. 여백 조정 및 A4 출력 최적화 포함.
/// </summary>
public class PrintPreviewForm : Form
{
    private readonly PrintDocument _printDoc;
    private PrintPreviewControl _previewControl = null!;
    private NumericUpDown _marginTop = null!;
    private NumericUpDown _marginBottom = null!;
    private NumericUpDown _marginLeft = null!;
    private NumericUpDown _marginRight = null!;
    private readonly RichTextBox _editor;
    private int _charIndex;

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

        _printDoc.PrintPage += PrintDoc_PrintPage;

        Text = "인쇄 미리보기 (A4 용지 기준)";
        Size = new Size(920, 720);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        BuildUI(initialMargins);
        UpdatePreview();
    }

    private void BuildUI(Margins? init)
    {
        // Top panel with margin controls and buttons
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10, 10, 10, 5),
            BackColor = SystemColors.Control
        };

        int topVal = init?.Top ?? 98;
        int botVal = init?.Bottom ?? 98;
        int leftVal = init?.Left ?? 79;
        int rightVal = init?.Right ?? 79;

        int x = 10;
        topPanel.Controls.Add(new Label { Text = "위쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginTop = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(topVal, 0, 300), DecimalPlaces = 0 };
        _marginTop.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginTop);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "아래:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginBottom = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(botVal, 0, 300), DecimalPlaces = 0 };
        _marginBottom.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginBottom);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "왼쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginLeft = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(leftVal, 0, 300), DecimalPlaces = 0 };
        _marginLeft.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginLeft);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "오른쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 45;
        _marginRight = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = Math.Clamp(rightVal, 0, 300), DecimalPlaces = 0 };
        _marginRight.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginRight);

        x += 75;
        var printBtn = new Button { Text = "인쇄", Location = new Point(x, 10), Width = 70, Height = 28 };
        printBtn.Click += PrintBtn_Click;
        topPanel.Controls.Add(printBtn);

        x += 80;
        var closeBtn = new Button { Text = "닫기", Location = new Point(x, 10), Width = 70, Height = 28 };
        closeBtn.Click += (s, e) => Close();
        topPanel.Controls.Add(closeBtn);

        Controls.Add(topPanel);

        // Preview control
        _previewControl = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Document = _printDoc,
            Zoom = 0.8,
            AutoZoom = false
        };
        Controls.Add(_previewControl);
        _previewControl.BringToFront();
    }

    private void Margin_Changed(object? sender, EventArgs e)
    {
        UpdatePreview();
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
        _previewControl.InvalidatePreview();
    }

    private void PrintDoc_PrintPage(object? sender, PrintPageEventArgs e)
    {
        RtfPrintHelper.PrintPage(_editor, e, ref _charIndex);
    }

    private void PrintBtn_Click(object? sender, EventArgs e)
    {
        using var printDlg = new PrintDialog { Document = _printDoc };
        if (printDlg.ShowDialog() == DialogResult.OK)
        {
            _charIndex = 0;
            _printDoc.Print();
            Close();
        }
    }
}
