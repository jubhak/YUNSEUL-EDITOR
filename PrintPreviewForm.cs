using System.Drawing.Printing;

namespace YunseulMemo;

/// <summary>
/// 인쇄 미리보기 폼. 여백 조정 기능 포함.
/// </summary>
public class PrintPreviewForm : Form
{
    private PrintDocument _printDoc;
    private PrintPreviewControl _previewControl = null!;
    private NumericUpDown _marginTop = null!;
    private NumericUpDown _marginBottom = null!;
    private NumericUpDown _marginLeft = null!;
    private NumericUpDown _marginRight = null!;
    private RichTextBox _editor;
    private int _charIndex;

    public PrintPreviewForm(RichTextBox editor)
    {
        _editor = editor;
        _printDoc = new PrintDocument();
        _printDoc.PrintPage += PrintDoc_PrintPage;

        Text = "인쇄 미리보기";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        BuildUI();
        UpdatePreview();
    }

    private void BuildUI()
    {
        // Top panel with margin controls and buttons
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10, 10, 10, 5),
            BackColor = SystemColors.Control
        };

        int x = 10;
        topPanel.Controls.Add(new Label { Text = "위쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginTop = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = 100, DecimalPlaces = 0 };
        _marginTop.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginTop);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "아래:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginBottom = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = 100, DecimalPlaces = 0 };
        _marginBottom.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginBottom);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "왼쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 40;
        _marginLeft = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = 100, DecimalPlaces = 0 };
        _marginLeft.ValueChanged += Margin_Changed;
        topPanel.Controls.Add(_marginLeft);

        x += 65;
        topPanel.Controls.Add(new Label { Text = "오른쪽:", Location = new Point(x, 15), AutoSize = true });
        x += 45;
        _marginRight = new NumericUpDown { Location = new Point(x, 12), Width = 55, Minimum = 0, Maximum = 300, Value = 100, DecimalPlaces = 0 };
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
