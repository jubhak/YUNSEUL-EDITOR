namespace YunseulMemo;

public class TableDialog : Form
{
    private NumericUpDown _rowsInput = null!;
    private NumericUpDown _colsInput = null!;

    public int Rows => (int)_rowsInput.Value;
    public int Cols => (int)_colsInput.Value;

    public TableDialog()
    {
        Text = "표 삽입";
        Size = new Size(300, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var rowLabel = new Label { Text = "행 수:", Location = new Point(16, 20), AutoSize = true };
        _rowsInput = new NumericUpDown { Location = new Point(80, 18), Width = 80, Minimum = 1, Maximum = 30, Value = 3 };

        var colLabel = new Label { Text = "열 수:", Location = new Point(16, 55), AutoSize = true };
        _colsInput = new NumericUpDown { Location = new Point(80, 53), Width = 80, Minimum = 1, Maximum = 20, Value = 3 };

        var infoLabel = new Label { Text = "첫 번째 행은 헤더로 만들어집니다.", Location = new Point(16, 88), AutoSize = true, ForeColor = Color.Gray };

        var okBtn = new Button { Text = "삽입", DialogResult = DialogResult.OK, Location = new Point(120, 110), Width = 75 };
        var cancelBtn = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(201, 110), Width = 75 };

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.AddRange(new Control[] { rowLabel, _rowsInput, colLabel, _colsInput, infoLabel, okBtn, cancelBtn });
    }
}
