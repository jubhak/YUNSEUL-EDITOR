using System.Drawing.Drawing2D;

namespace YunseulMemo;

/// <summary>
/// 고급 표 삽입 및 표 속성/서식 변경 대화상자.
/// 행/열 수, 선 굵기, 선 색상, 투명선, 컬럼별 글자 정렬, 헤더 스타일 및 실시간 미리보기를 지원합니다.
/// </summary>
public sealed class TableDialog : Form
{
    private readonly NumericUpDown _nudRows;
    private readonly NumericUpDown _nudCols;
    private readonly ComboBox _cbBorderWidth;
    private readonly CheckBox _chkTransparent;
    private readonly Panel _pnlBorderColor;
    private readonly Button _btnChooseColor;
    private readonly ComboBox _cbGlobalAlign;
    private readonly ListView _lvColAligns;
    private readonly CheckBox _chkHeaderRow;
    private readonly Panel _pnlHeaderColor;
    private readonly Button _btnHeaderColor;
    private readonly Panel _pnlPreview;

    private Color _borderColor = Color.FromArgb(203, 213, 225); // Slate-300
    private Color _headerBgColor = Color.FromArgb(241, 245, 249); // Slate-100
    private readonly bool _isModifyMode;

    public int Rows => (int)_nudRows.Value;
    public int Cols => (int)_nudCols.Value;
    public int BorderWidth => _chkTransparent.Checked ? 0 : _cbBorderWidth.SelectedIndex;
    public Color BorderColor => _borderColor;
    public bool IsTransparentBorder => _chkTransparent.Checked || _cbBorderWidth.SelectedIndex == 0;
    public bool HasHeaderRow => _chkHeaderRow.Checked;
    public Color HeaderBgColor => _headerBgColor;

    public HorizontalAlignment[] Alignments
    {
        get
        {
            int cols = Cols;
            var result = new HorizontalAlignment[cols];
            for (int i = 0; i < cols; i++)
            {
                if (i < _lvColAligns.Items.Count && _lvColAligns.Items[i].Tag is HorizontalAlignment align)
                {
                    result[i] = align;
                }
                else
                {
                    result[i] = GetGlobalAlignment();
                }
            }
            return result;
        }
    }

    public TableStyleOptions Options => new()
    {
        Rows = Rows,
        Cols = Cols,
        BorderWidth = BorderWidth,
        BorderColor = BorderColor,
        IsTransparentBorder = IsTransparentBorder,
        Alignments = Alignments,
        HasHeaderRow = HasHeaderRow,
        HeaderBgColor = HeaderBgColor
    };

    public TableDialog(bool isModifyMode = false, TableStyleOptions? initialOptions = null)
    {
        _isModifyMode = isModifyMode;

        Text = _isModifyMode ? "표 속성 및 서식 변경" : "표 삽입";
        ClientSize = new Size(660, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(248, 250, 252);

        // 1. 컨트롤 인스턴스 생성
        _nudRows = new NumericUpDown
        {
            Location = new Point(62, 27),
            Width = 65,
            Minimum = 1,
            Maximum = 50,
            Value = initialOptions?.Rows ?? 3,
            Font = new Font("Segoe UI", 9.5f)
        };
        _nudCols = new NumericUpDown
        {
            Location = new Point(208, 27),
            Width = 65,
            Minimum = 1,
            Maximum = 20,
            Value = initialOptions?.Cols ?? 3,
            Font = new Font("Segoe UI", 9.5f)
        };

        _cbBorderWidth = new ComboBox
        {
            Location = new Point(78, 25),
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cbBorderWidth.Items.AddRange(["투명선 (없음)", "얇은 선 (1pt)", "보통 선 (2pt)", "굵은 선 (3pt)"]);
        _cbBorderWidth.SelectedIndex = initialOptions != null ? Math.Clamp(initialOptions.BorderWidth, 0, 3) : 1;

        _chkTransparent = new CheckBox
        {
            Text = "투명선 (선 숨기기)",
            Location = new Point(200, 27),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Checked = initialOptions?.IsTransparentBorder ?? false
        };

        _borderColor = initialOptions?.BorderColor ?? Color.FromArgb(203, 213, 225);
        _pnlBorderColor = new Panel
        {
            Location = new Point(78, 66),
            Size = new Size(40, 24),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _borderColor
        };

        _btnChooseColor = new Button
        {
            Text = "색상 변경...",
            Location = new Point(126, 65),
            Width = 90,
            Height = 26,
            Font = new Font("Segoe UI", 8.5f)
        };

        _cbGlobalAlign = new ComboBox
        {
            Location = new Point(78, 22),
            Width = 90,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8.5f)
        };
        _cbGlobalAlign.Items.AddRange(["왼쪽 정렬", "가운데 정렬", "오른쪽 정렬"]);
        _cbGlobalAlign.SelectedIndex = 0;

        _lvColAligns = new ListView
        {
            Location = new Point(14, 54),
            Size = new Size(302, 95),
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            Font = new Font("Segoe UI", 8.5f)
        };
        _lvColAligns.Columns.Add("열", 80);
        _lvColAligns.Columns.Add("정렬 방식", 190);

        _chkHeaderRow = new CheckBox
        {
            Text = "첫 행 헤더 스타일",
            Location = new Point(14, 22),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Checked = initialOptions?.HasHeaderRow ?? true
        };

        _headerBgColor = initialOptions?.HeaderBgColor ?? Color.FromArgb(241, 245, 249);
        _pnlHeaderColor = new Panel
        {
            Location = new Point(14, 46),
            Size = new Size(32, 20),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _headerBgColor
        };

        _btnHeaderColor = new Button
        {
            Text = "헤더 배경색...",
            Location = new Point(52, 44),
            Width = 100,
            Height = 24,
            Font = new Font("Segoe UI", 8f)
        };

        _pnlPreview = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // 2. 레이아웃 배치
        int leftX = 16;
        int curY = 16;

        var grpSize = new GroupBox
        {
            Text = "표 크기",
            Location = new Point(leftX, curY),
            Size = new Size(330, 75),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var lblRows = new Label { Text = "행 수:", Location = new Point(14, 30), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        var lblCols = new Label { Text = "열 수:", Location = new Point(160, 30), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        grpSize.Controls.AddRange([lblRows, _nudRows, lblCols, _nudCols]);

        curY += 85;
        var grpBorder = new GroupBox
        {
            Text = "테두리 선 스타일",
            Location = new Point(leftX, curY),
            Size = new Size(330, 115),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var lblBorderWidth = new Label { Text = "선 굵기:", Location = new Point(14, 28), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        var lblBorderColor = new Label { Text = "선 색상:", Location = new Point(14, 68), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        grpBorder.Controls.AddRange([lblBorderWidth, _cbBorderWidth, _chkTransparent, lblBorderColor, _pnlBorderColor, _btnChooseColor]);

        curY += 125;
        var grpAlign = new GroupBox
        {
            Text = "컬럼별 글자 정렬",
            Location = new Point(leftX, curY),
            Size = new Size(330, 160),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var lblGlobalAlign = new Label { Text = "전체 기본:", Location = new Point(14, 25), AutoSize = true, Font = new Font("Segoe UI", 9f) };
        var btnCycleCol = new Button
        {
            Text = "선택 열 정렬 변경",
            Location = new Point(180, 21),
            Width = 135,
            Height = 25,
            Font = new Font("Segoe UI", 8.5f)
        };
        grpAlign.Controls.AddRange([lblGlobalAlign, _cbGlobalAlign, btnCycleCol, _lvColAligns]);

        int rightX = 360;
        var grpHeader = new GroupBox
        {
            Text = "헤더 및 배경 스타일",
            Location = new Point(rightX, 16),
            Size = new Size(275, 75),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        grpHeader.Controls.AddRange([_chkHeaderRow, _pnlHeaderColor, _btnHeaderColor]);

        var grpPreview = new GroupBox
        {
            Text = "실시간 미리보기",
            Location = new Point(rightX, 101),
            Size = new Size(275, 300),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        grpPreview.Controls.Add(_pnlPreview);

        var okBtn = new Button
        {
            Text = _isModifyMode ? "서식 적용" : "표 삽입",
            DialogResult = DialogResult.OK,
            Location = new Point(rightX + 85, 415),
            Width = 95,
            Height = 35,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        okBtn.FlatAppearance.BorderSize = 0;

        var cancelBtn = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            Location = new Point(rightX + 190, 415),
            Width = 85,
            Height = 35,
            Font = new Font("Segoe UI", 9.5f),
            FlatStyle = FlatStyle.Flat
        };

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.AddRange([grpSize, grpBorder, grpAlign, grpHeader, grpPreview, okBtn, cancelBtn]);

        // 3. 이벤트 핸들러 바인딩
        _nudRows.ValueChanged += (_, _) => { RefreshColAlignList(); _pnlPreview.Invalidate(); };
        _nudCols.ValueChanged += (_, _) => { RefreshColAlignList(); _pnlPreview.Invalidate(); };

        _cbBorderWidth.SelectedIndexChanged += (_, _) =>
        {
            if (_cbBorderWidth.SelectedIndex == 0) _chkTransparent.Checked = true;
            _pnlPreview.Invalidate();
        };

        _chkTransparent.CheckedChanged += (_, _) =>
        {
            _cbBorderWidth.Enabled = !_chkTransparent.Checked;
            _btnChooseColor.Enabled = !_chkTransparent.Checked;
            _pnlBorderColor.Enabled = !_chkTransparent.Checked;
            _pnlPreview.Invalidate();
        };

        _btnChooseColor.Click += (_, _) =>
        {
            using var cd = new ColorDialog { Color = _borderColor };
            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                _borderColor = cd.Color;
                _pnlBorderColor.BackColor = _borderColor;
                _pnlPreview.Invalidate();
            }
        };

        _cbGlobalAlign.SelectedIndexChanged += (_, _) =>
        {
            ApplyGlobalAlignToAll();
            _pnlPreview.Invalidate();
        };

        btnCycleCol.Click += (_, _) => CycleSelectedColAlign();
        _lvColAligns.DoubleClick += (_, _) => CycleSelectedColAlign();

        _chkHeaderRow.CheckedChanged += (_, _) =>
        {
            _pnlHeaderColor.Enabled = _chkHeaderRow.Checked;
            _btnHeaderColor.Enabled = _chkHeaderRow.Checked;
            _pnlPreview.Invalidate();
        };

        _btnHeaderColor.Click += (_, _) =>
        {
            using var cd = new ColorDialog { Color = _headerBgColor };
            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                _headerBgColor = cd.Color;
                _pnlHeaderColor.BackColor = _headerBgColor;
                _pnlPreview.Invalidate();
            }
        };

        _pnlPreview.Paint += DrawPreviewTable;

        // 초기 상태 동기화
        _cbBorderWidth.Enabled = !_chkTransparent.Checked;
        _btnChooseColor.Enabled = !_chkTransparent.Checked;
        _pnlBorderColor.Enabled = !_chkTransparent.Checked;
        _pnlHeaderColor.Enabled = _chkHeaderRow.Checked;
        _btnHeaderColor.Enabled = _chkHeaderRow.Checked;

        RefreshColAlignList();
        if (initialOptions?.Alignments != null && initialOptions.Alignments.Length > 0)
        {
            for (int i = 0; i < Math.Min(initialOptions.Alignments.Length, _lvColAligns.Items.Count); i++)
            {
                SetColAlign(i, initialOptions.Alignments[i]);
            }
        }
    }

    private HorizontalAlignment GetGlobalAlignment()
    {
        return _cbGlobalAlign.SelectedIndex switch
        {
            1 => HorizontalAlignment.Center,
            2 => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
    }

    private void ApplyGlobalAlignToAll()
    {
        var align = GetGlobalAlignment();
        for (int i = 0; i < _lvColAligns.Items.Count; i++)
        {
            SetColAlign(i, align);
        }
    }

    private void RefreshColAlignList()
    {
        int cols = (int)_nudCols.Value;
        var existing = new List<HorizontalAlignment>();
        foreach (ListViewItem item in _lvColAligns.Items)
        {
            existing.Add((HorizontalAlignment)(item.Tag ?? HorizontalAlignment.Left));
        }

        _lvColAligns.Items.Clear();
        var globalAlign = GetGlobalAlignment();

        for (int c = 0; c < cols; c++)
        {
            var align = (c < existing.Count) ? existing[c] : globalAlign;
            var item = new ListViewItem($"열 {c + 1}");
            item.SubItems.Add(AlignToString(align));
            item.Tag = align;
            _lvColAligns.Items.Add(item);
        }
    }

    private void CycleSelectedColAlign()
    {
        if (_lvColAligns.SelectedItems.Count == 0) return;
        var item = _lvColAligns.SelectedItems[0];
        var cur = (HorizontalAlignment)(item.Tag ?? HorizontalAlignment.Left);
        var next = cur switch
        {
            HorizontalAlignment.Left => HorizontalAlignment.Center,
            HorizontalAlignment.Center => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
        SetColAlign(item.Index, next);
        _pnlPreview.Invalidate();
    }

    private void SetColAlign(int index, HorizontalAlignment align)
    {
        if (index < 0 || index >= _lvColAligns.Items.Count) return;
        var item = _lvColAligns.Items[index];
        item.Tag = align;
        item.SubItems[1].Text = AlignToString(align);
    }

    private static string AlignToString(HorizontalAlignment a) => a switch
    {
        HorizontalAlignment.Center => "가운데 정렬",
        HorizontalAlignment.Right => "오른쪽 정렬",
        _ => "왼쪽 정렬"
    };

    private void DrawPreviewTable(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int rows = Math.Min(Rows, 8);
        int cols = Math.Min(Cols, 6);
        if (rows <= 0 || cols <= 0) return;

        var rect = new Rectangle(12, 12, _pnlPreview.Width - 24, _pnlPreview.Height - 24);
        int cellW = rect.Width / cols;
        int cellH = Math.Min(32, rect.Height / rows);

        var aligns = Alignments;

        for (int r = 0; r < rows; r++)
        {
            bool isHeader = (r == 0 && HasHeaderRow);
            for (int c = 0; c < cols; c++)
            {
                var cellRect = new Rectangle(rect.Left + c * cellW, rect.Top + r * cellH, cellW, cellH);

                if (isHeader)
                {
                    using var brHeader = new SolidBrush(HeaderBgColor);
                    g.FillRectangle(brHeader, cellRect);
                }
                else
                {
                    using var brCell = new SolidBrush(Color.White);
                    g.FillRectangle(brCell, cellRect);
                }

                if (!IsTransparentBorder && BorderWidth > 0)
                {
                    using var pen = new Pen(BorderColor, BorderWidth);
                    g.DrawRectangle(pen, cellRect);
                }
                else if (IsTransparentBorder)
                {
                    using var dotPen = new Pen(Color.FromArgb(160, 203, 213, 225), 1) { DashStyle = DashStyle.Dot };
                    g.DrawRectangle(dotPen, cellRect);
                }

                string text = isHeader ? $"열 {c + 1}" : $"데이터";
                var align = (c < aligns.Length) ? aligns[c] : HorizontalAlignment.Left;
                using var font = new Font("Segoe UI", 8f, isHeader ? FontStyle.Bold : FontStyle.Regular);
                using var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = align switch
                    {
                        HorizontalAlignment.Center => StringAlignment.Center,
                        HorizontalAlignment.Right => StringAlignment.Far,
                        _ => StringAlignment.Near
                    }
                };
                using var textBrush = new SolidBrush(isHeader ? Color.FromArgb(15, 23, 42) : Color.FromArgb(71, 85, 105));
                var textRect = new Rectangle(cellRect.Left + 4, cellRect.Top, cellRect.Width - 8, cellRect.Height);
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }
    }
}
