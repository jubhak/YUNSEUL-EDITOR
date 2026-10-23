namespace YunseulMemo;

/// <summary>
/// 탭/문서 닫기 시 미저장 변경사항 확인 대화상자.
/// [저장하기], [무시하고 닫기], [닫지 않기] 3가지 선택지를 명확하게 제공합니다.
/// </summary>
public sealed class CloseConfirmDialog : Form
{
    public enum ConfirmAction
    {
        SaveAndClose, // 저장하기
        DiscardAndClose, // 무시하고 닫기
        Cancel // 닫지 않기 (중단)
    }

    public ConfirmAction SelectedAction { get; private set; } = ConfirmAction.Cancel;

    public CloseConfirmDialog(string docTitle, bool isNewDocument)
    {
        Text = "윤슬 에디터";
        ClientSize = new Size(460, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f);

        // 아이콘
        var iconBox = new PictureBox
        {
            Location = new Point(24, 28),
            Size = new Size(36, 36),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Image = SystemIcons.Question.ToBitmap()
        };

        // 타이틀 문구
        string mainMsg = isNewDocument
            ? $"'{docTitle}'에 입력된 내용이 있습니다."
            : $"'{docTitle}'에 수정된 내용이 있습니다.";

        var lblMain = new Label
        {
            Text = mainMsg,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(72, 26),
            Size = new Size(365, 30),
            AutoEllipsis = true
        };

        var lblSub = new Label
        {
            Text = "변경 내용을 저장하시겠습니까?\n저장하지 않고 닫으려면 [무시하고 닫기]를 선택하세요.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(72, 60),
            Size = new Size(365, 45)
        };

        // 하단 버튼 패널
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(16, 12, 16, 12)
        };
        btnPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, btnPanel.Width, 0);
        };

        // [저장하기] 버튼
        var btnSave = new Button
        {
            Text = "저장하기(&S)",
            Size = new Size(100, 36),
            Location = new Point(115, 14),
            BackColor = Color.FromArgb(37, 99, 235), // 파란색 액센트
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            SelectedAction = ConfirmAction.SaveAndClose;
            DialogResult = DialogResult.OK;
            Close();
        };

        // [무시하고 닫기] 버튼
        var btnDiscard = new Button
        {
            Text = "무시하고 닫기(&D)",
            Size = new Size(125, 36),
            Location = new Point(223, 14),
            BackColor = Color.FromArgb(239, 68, 68), // 레드 액센트
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDiscard.FlatAppearance.BorderSize = 0;
        btnDiscard.Click += (_, _) =>
        {
            SelectedAction = ConfirmAction.DiscardAndClose;
            DialogResult = DialogResult.OK;
            Close();
        };

        // [닫지 않기] 버튼
        var btnCancel = new Button
        {
            Text = "닫지 않기(&C)",
            Size = new Size(95, 36),
            Location = new Point(354, 14),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (_, _) =>
        {
            SelectedAction = ConfirmAction.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        btnPanel.Controls.AddRange([btnSave, btnDiscard, btnCancel]);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.AddRange([iconBox, lblMain, lblSub, btnPanel]);
    }
}
