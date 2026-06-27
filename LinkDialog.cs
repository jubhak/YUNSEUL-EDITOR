namespace YunseulMemo;

public class LinkDialog : Form
{
    private TextBox _textBox = null!;
    private TextBox _urlBox = null!;

    public string LinkText => _textBox.Text.Trim();
    public string LinkUrl => _urlBox.Text.Trim();

    public LinkDialog()
    {
        Text = "링크 삽입";
        Size = new Size(400, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var textLabel = new Label { Text = "표시 텍스트:", Location = new Point(16, 20), AutoSize = true };
        _textBox = new TextBox { Location = new Point(16, 40), Width = 350 };

        var urlLabel = new Label { Text = "URL:", Location = new Point(16, 72), AutoSize = true };
        _urlBox = new TextBox { Location = new Point(16, 92), Width = 350, Text = "https://" };

        var okBtn = new Button { Text = "삽입", Location = new Point(210, 125), Width = 75 };
        okBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_urlBox.Text))
            {
                MessageBox.Show("URL을 입력하세요.", "링크 삽입", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            DialogResult = DialogResult.OK;
        };
        var cancelBtn = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(291, 125), Width = 75 };

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.AddRange(new Control[] { textLabel, _textBox, urlLabel, _urlBox, okBtn, cancelBtn });
    }
}
