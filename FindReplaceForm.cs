using System.Text.RegularExpressions;

namespace YunseulMemo;

/// <summary>
/// EditPlus 스타일의 모달리스 찾기/바꾸기 대화상자.
/// 대/소문자 구분, 단어 단위, 정규식, 위/아래 방향, 선택 영역 내 바꾸기를 지원합니다.
/// </summary>
public sealed class FindReplaceForm : Form
{
    private readonly Func<RichTextBox?> _getEditor;
    private readonly ComboBox _findBox = new();
    private readonly ComboBox _replaceBox = new();
    private readonly Label _replaceLabel = new();
    private readonly CheckBox _caseChk = new();
    private readonly CheckBox _wordChk = new();
    private readonly CheckBox _regexChk = new();
    private readonly CheckBox _selChk = new();
    private readonly RadioButton _downRadio = new();
    private readonly RadioButton _upRadio = new();
    private readonly Button _findBtn = new();
    private readonly Button _replaceBtn = new();
    private readonly Button _replaceAllBtn = new();
    private readonly Button _closeBtn = new();
    private readonly Label _msg = new();
    private readonly GroupBox _dirGroup = new();
    private bool _replaceMode;

    /// <summary>상태 표시줄에 보낼 메시지.</summary>
    public event Action<string>? StatusMessage;

    public string FindText => _findBox.Text;

    public FindReplaceForm(Func<RichTextBox?> getEditor)
    {
        _getEditor = getEditor;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("맑은 고딕", 9f);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var findLabel = new Label { Text = "찾을 내용(&N):", AutoSize = true, Location = new Point(12, 16) };
        _findBox.Location = new Point(100, 12); _findBox.Width = 250;
        _replaceLabel.Text = "바꿀 내용(&P):"; _replaceLabel.AutoSize = true; _replaceLabel.Location = new Point(12, 46);
        _replaceBox.Location = new Point(100, 42); _replaceBox.Width = 250;

        _caseChk.Text = "대/소문자 구분(&C)"; _caseChk.AutoSize = true; _caseChk.Location = new Point(14, 78);
        _wordChk.Text = "단어 단위로(&W)"; _wordChk.AutoSize = true; _wordChk.Location = new Point(14, 100);
        _regexChk.Text = "정규식(&E)"; _regexChk.AutoSize = true; _regexChk.Location = new Point(14, 122);
        _selChk.Text = "선택 영역에서만 바꾸기(&S)"; _selChk.AutoSize = true; _selChk.Location = new Point(14, 144);

        _dirGroup.Text = "방향"; _dirGroup.Location = new Point(190, 72); _dirGroup.Size = new Size(160, 52);
        _downRadio.Text = "아래로(&D)"; _downRadio.AutoSize = true; _downRadio.Checked = true; _downRadio.Location = new Point(10, 20);
        _upRadio.Text = "위로(&U)"; _upRadio.AutoSize = true; _upRadio.Location = new Point(88, 20);
        _dirGroup.Controls.AddRange(new Control[] { _downRadio, _upRadio });

        _findBtn.Text = "다음 찾기(&F)"; _findBtn.Location = new Point(366, 10); _findBtn.Size = new Size(96, 26);
        _replaceBtn.Text = "바꾸기(&R)"; _replaceBtn.Location = new Point(366, 42); _replaceBtn.Size = new Size(96, 26);
        _replaceAllBtn.Text = "모두 바꾸기(&A)"; _replaceAllBtn.Location = new Point(366, 74); _replaceAllBtn.Size = new Size(96, 26);
        _closeBtn.Text = "닫기"; _closeBtn.Location = new Point(366, 106); _closeBtn.Size = new Size(96, 26);

        _msg.Location = new Point(12, 172); _msg.Size = new Size(450, 20); _msg.ForeColor = Color.FromArgb(180, 40, 40);

        AcceptButton = _findBtn;
        CancelButton = _closeBtn;

        Controls.AddRange(new Control[] { findLabel, _findBox, _replaceLabel, _replaceBox, _caseChk, _wordChk, _regexChk, _selChk,
            _dirGroup, _findBtn, _replaceBtn, _replaceAllBtn, _closeBtn, _msg });

        _findBtn.Click += (_, _) => FindNext(_downRadio.Checked);
        _replaceBtn.Click += (_, _) => ReplaceOne();
        _replaceAllBtn.Click += (_, _) => ReplaceAll();
        _closeBtn.Click += (_, _) => Hide();

        SetMode(false);
    }

    /// <summary>찾기(false) 또는 바꾸기(true) 모드로 표시합니다.</summary>
    public void ShowFind(IWin32Window owner, string initialText, bool replace)
    {
        SetMode(replace);
        if (!string.IsNullOrEmpty(initialText) && !initialText.Contains('\n'))
            _findBox.Text = initialText;
        _msg.Text = "";
        if (!Visible) Show(owner); else Activate();
        _findBox.Focus();
        _findBox.SelectAll();
    }

    private void SetMode(bool replace)
    {
        _replaceMode = replace;
        Text = replace ? "바꾸기" : "찾기";
        _replaceLabel.Visible = _replaceBox.Visible = replace;
        _replaceBtn.Visible = _replaceAllBtn.Visible = replace;
        _selChk.Visible = replace;
        _closeBtn.Top = replace ? 106 : 42;
        _msg.Top = replace ? 172 : 150;
        _caseChk.Top = replace ? 78 : 48; _wordChk.Top = replace ? 100 : 70; _regexChk.Top = replace ? 122 : 92;
        _dirGroup.Top = replace ? 72 : 42;
        ClientSize = new Size(476, replace ? 200 : 178);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 사용자가 닫을 때(X, Esc, 닫기 버튼)는 파괴하지 않고 숨겨서 검색어/옵션을 유지합니다.
        // 주 창이 닫히거나 앱이 종료될 때는 그대로 닫습니다.
        bool appClosing = e.CloseReason is CloseReason.FormOwnerClosing or CloseReason.ApplicationExitCall
            or CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing;
        if (!appClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }

    // ───────────── 검색 로직 ─────────────

    private Regex? BuildRegex()
    {
        string text = _findBox.Text;
        if (string.IsNullOrEmpty(text)) { Say("찾을 내용을 입력하세요."); return null; }

        string pattern = _regexChk.Checked ? text : Regex.Escape(text);
        if (_wordChk.Checked) pattern = $@"\b(?:{pattern})\b";
        var opts = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        if (!_caseChk.Checked) opts |= RegexOptions.IgnoreCase;
        try { return new Regex(pattern, opts); }
        catch (ArgumentException ex) { Say("정규식 오류: " + ex.Message); return null; }
    }

    private void Remember(ComboBox box)
    {
        string t = box.Text;
        if (string.IsNullOrEmpty(t)) return;
        box.Items.Remove(t);
        box.Items.Insert(0, t);
        while (box.Items.Count > 20) box.Items.RemoveAt(box.Items.Count - 1);
        box.Text = t;
    }

    private void Say(string msg)
    {
        _msg.Text = msg;
        StatusMessage?.Invoke(msg);
    }

    /// <summary>다음(또는 이전) 항목을 찾아 선택합니다. 끝에 도달하면 반대쪽에서 계속합니다.</summary>
    public bool FindNext(bool forward)
    {
        var ed = _getEditor();
        if (ed == null) return false;
        var rx = BuildRegex();
        if (rx == null) return false;
        Remember(_findBox);

        string text = ed.Text;
        Match? found = null;
        bool wrapped = false;

        if (forward)
        {
            found = NextNonEmpty(rx, text, ed.SelectionStart + ed.SelectionLength);
            if (found == null) { found = NextNonEmpty(rx, text, 0); wrapped = found != null; }
        }
        else
        {
            int limit = ed.SelectionStart;
            foreach (Match m in rx.Matches(text))
            {
                if (m.Length == 0) continue;
                if (m.Index + m.Length <= limit) found = m; else break;
            }
            if (found == null)
            {
                foreach (Match m in rx.Matches(text)) if (m.Length > 0) found = m;
                wrapped = found != null;
            }
        }

        if (found == null) { Say("찾을 수 없습니다: " + _findBox.Text); return false; }

        ed.Select(found.Index, found.Length);
        ed.ScrollToCaret();
        Say(wrapped ? (forward ? "문서 끝까지 검색하여 처음부터 계속합니다." : "문서 처음까지 검색하여 끝에서부터 계속합니다.") : "");
        return true;
    }

    private static Match? NextNonEmpty(Regex rx, string text, int start)
    {
        if (start > text.Length) return null;
        var m = rx.Match(text, start);
        while (m.Success && m.Length == 0) m = m.NextMatch();
        return m.Success ? m : null;
    }

    private string Expand(Match m) => _regexChk.Checked ? m.Result(_replaceBox.Text) : _replaceBox.Text;

    private void ReplaceOne()
    {
        var ed = _getEditor();
        if (ed == null) return;
        var rx = BuildRegex();
        if (rx == null) return;
        Remember(_replaceBox);

        // 현재 선택 영역이 검색어와 일치하면 바꾸고 다음으로 이동
        if (ed.SelectionLength > 0)
        {
            string sel = ed.SelectedText;
            var m = rx.Match(sel);
            if (m.Success && m.Index == 0 && m.Length == sel.Length)
                ed.SelectedText = Expand(m);
        }
        FindNext(true);
    }

    private void ReplaceAll()
    {
        var ed = _getEditor();
        if (ed == null) return;
        var rx = BuildRegex();
        if (rx == null) return;
        Remember(_findBox);
        Remember(_replaceBox);

        string text = ed.Text;
        int rangeStart = 0, rangeEnd = text.Length;
        if (_selChk.Checked && ed.SelectionLength > 0)
        {
            rangeStart = ed.SelectionStart;
            rangeEnd = rangeStart + ed.SelectionLength;
        }

        var matches = new List<Match>();
        foreach (Match m in rx.Matches(text))
            if (m.Length > 0 && m.Index >= rangeStart && m.Index + m.Length <= rangeEnd) matches.Add(m);

        if (matches.Count == 0) { Say("찾을 수 없습니다: " + _findBox.Text); return; }

        ed.SuspendLayout();
        try
        {
            // 뒤에서부터 바꾸면 앞쪽 인덱스가 유지됩니다.
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                ed.Select(m.Index, m.Length);
                ed.SelectedText = Expand(m);
            }
        }
        finally { ed.ResumeLayout(); }
        ed.Select(matches[0].Index, 0);
        Say($"{matches.Count}개 항목을 바꿨습니다.");
    }
}

/// <summary>줄 이동 대화상자 (Ctrl+G).</summary>
public sealed class GoToLineDialog : Form
{
    private readonly NumericUpDown _num = new();
    public int Line => (int)_num.Value;

    public GoToLineDialog(int current, int max)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("맑은 고딕", 9f);
        Text = "줄 이동";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(250, 112);

        var label = new Label { Text = $"줄 번호(&L) (1 - {max:N0}):", AutoSize = true, Location = new Point(12, 14) };
        _num.Location = new Point(14, 36); _num.Width = 220;
        _num.Minimum = 1; _num.Maximum = Math.Max(1, max);
        _num.Value = Math.Clamp(current, 1, Math.Max(1, max));
        var ok = new Button { Text = "이동", DialogResult = DialogResult.OK, Location = new Point(76, 72), Size = new Size(76, 26) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(158, 72), Size = new Size(76, 26) };
        AcceptButton = ok; CancelButton = cancel;
        Controls.AddRange(new Control[] { label, _num, ok, cancel });
        Shown += (_, _) => { _num.Select(0, _num.Text.Length); };
    }
}
