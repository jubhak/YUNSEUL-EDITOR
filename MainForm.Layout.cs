using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace YunseulMemo;

/// <summary>
/// 메인 창의 EditPlus 스타일 레이아웃:
/// 메뉴 → 표준/서식 도구 모음 → [디렉토리 창 | (문서 탭 → 눈금자 → 줄 번호+편집창)] → 상태 표시줄
/// </summary>
public partial class MainForm
{
    // ── 레이아웃 컨트롤 ──
    private MenuStrip _menuStrip = null!;
    private ToolStrip _mainToolStrip = null!;
    private ToolStrip _formatToolStrip = null!;
    private SplitContainer _splitContainer = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripStatusLabel _wordCountLabel = null!;
    private ToolStripStatusLabel _lineColLabel = null!;
    private ToolStripStatusLabel _eolLabel = null!;
    private ToolStripStatusLabel _encodingLabel = null!;

    // 디렉토리 창 (왼쪽)
    private TabControl _leftTabs = null!;
    private ComboBox _driveCombo = null!;
    private TreeView _folderTree = null!;
    private ListView _fileList = null!;
    private ComboBox _filterCombo = null!;
    private ListBox _clipList = null!;
    private ImageList _treeImages = null!;
    private ImageList _fileImages = null!;

    // 편집 영역
    private DocumentTabStrip _docTabs = null!;
    private RulerPanel _ruler = null!;
    private Panel _editorHost = null!;
    private LineNumberPanel _lineNumberPanel = null!;

    // 서식 도구 모음
    private ToolStripComboBox _fontFamilyCombo = null!;
    private ToolStripComboBox _fontSizeCombo = null!;
    private ToolStripComboBox _paragraphCombo = null!;
    private ToolStripButton _boldBtn = null!;
    private ToolStripButton _italicBtn = null!;
    private ToolStripButton _underlineBtn = null!;
    private ToolStripButton _strikeBtn = null!;
    private ToolStripButton _alignLeftBtn = null!;
    private ToolStripButton _alignCenterBtn = null!;
    private ToolStripButton _alignRightBtn = null!;
    private ToolStripButton _bulletListBtn = null!;
    private ToolStripButton _indentBtn = null!;
    private ToolStripButton _outdentBtn = null!;
    private ToolStripButton _textColorBtn = null!;
    private ToolStripButton _bgColorBtn = null!;

    // 표준 도구 모음 토글 버튼 / 메뉴 상태 항목
    private ToolStripButton _wrapBtn = null!;
    private ToolStripButton _lineNumBtn = null!;
    private ToolStripButton _rulerBtn = null!;
    private ToolStripMenuItem _miWrap = null!, _miRuler = null!, _miLineNum = null!, _miDirPane = null!;
    private ToolStripMenuItem _miStatusBar = null!, _miMainTool = null!, _miFormatTool = null!;
    private ToolStripMenuItem _recentMenu = null!, _windowMenu = null!, _docMenu = null!;
    private readonly List<ToolStripMenuItem> _encItems = new();
    private readonly List<ToolStripMenuItem> _eolItems = new();

    private Color _currentTextColor = Color.Black;
    private Color _currentBgColor = Color.Yellow;
    private Font _uiFont = null!;

    private int S(int v) => (int)Math.Round(v * Math.Max(96, DeviceDpi) / 96.0);
    private int IconSize => S(16);
    private Bitmap Img(Ico k, Color? c = null) => ToolIcons.Create(k, IconSize, c);

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private static Icon MakeAppIcon()
    {
        using var bmp = ToolIcons.Create(Ico.Page, 32, Color.FromArgb(60, 120, 210));
        IntPtr h = bmp.GetHicon();
        try { using var tmp = Icon.FromHandle(h); return (Icon)tmp.Clone(); }
        finally { DestroyIcon(h); }
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        _uiFont = new Font("맑은 고딕", 9f);
        Font = _uiFont;
        Text = _appName;
        ClientSize = new Size(S(1100), S(720));
        StartPosition = FormStartPosition.CenterScreen;
        Icon = MakeAppIcon();
        KeyPreview = true;
        AllowDrop = true;
        KeyDown += MainForm_KeyDown;
        Load += MainForm_Load;
        DragEnter += Files_DragEnter;
        DragDrop += Files_DragDrop;

        ToolStripManager.Renderer = new EditPlusRenderer();

        _menuStrip = new MenuStrip { Dock = DockStyle.Fill, Font = _uiFont, GripStyle = ToolStripGripStyle.Hidden, Margin = Padding.Empty };
        _menuStrip.ImageScalingSize = new Size(IconSize, IconSize);
        BuildMenu();
        MainMenuStrip = _menuStrip;

        _mainToolStrip = NewToolStrip();
        BuildMainToolStrip();
        _formatToolStrip = NewToolStrip();
        BuildFormatToolStrip();

        BuildStatusStrip();

        // 메인 분할: 왼쪽 디렉토리 창 / 오른쪽 편집 영역
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = S(4),
            FixedPanel = FixedPanel.Panel1,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            Margin = new Padding(0),
            SplitterDistance = S(200)
        };

        BuildLeftPane();
        BuildEditorArea();

        // 루트 레이아웃: 위에서 아래로 정확한 순서가 보장되는 TableLayoutPanel 사용
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_menuStrip, 0, 0);
        root.Controls.Add(_mainToolStrip, 0, 1);
        root.Controls.Add(_formatToolStrip, 0, 2);
        root.Controls.Add(_splitContainer, 0, 3);
        root.Controls.Add(_statusStrip, 0, 4);
        Controls.Add(root);

        ResumeLayout(false);
        PerformLayout();
    }

    private ToolStrip NewToolStrip() => new ToolStrip
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        Font = _uiFont,
        GripStyle = ToolStripGripStyle.Visible,
        ImageScalingSize = new Size(IconSize, IconSize),
        Padding = new Padding(2, 1, 2, 1),
        Margin = Padding.Empty
    };

    // ───────────────────────── 메뉴 ─────────────────────────

    private ToolStripMenuItem Mi(string text, Keys shortcut, EventHandler handler, Ico? icon = null)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
        if (icon.HasValue) item.Image = Img(icon.Value);
        item.Click += handler;
        return item;
    }

    private void BuildMenu()
    {
        // 파일
        var file = new ToolStripMenuItem("파일(&F)");
        file.DropDownItems.Add(Mi("새로 만들기(&N)", Keys.Control | Keys.N, OnNew, Ico.New));
        file.DropDownItems.Add(Mi("열기(&O)...", Keys.Control | Keys.O, OnOpen, Ico.Open));
        _recentMenu = new ToolStripMenuItem("최근 파일(&R)");
        _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(없음)") { Enabled = false });
        _recentMenu.DropDownOpening += (_, _) => RebuildRecentMenu();
        file.DropDownItems.Add(_recentMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Mi("닫기(&C)", Keys.Control | Keys.W, OnCloseTab));
        file.DropDownItems.Add(Mi("모두 닫기(&L)", Keys.None, OnCloseAll));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Mi("저장(&S)", Keys.Control | Keys.S, OnSave, Ico.Save));
        file.DropDownItems.Add(Mi("다른 이름으로 저장(&A)...", Keys.Control | Keys.Shift | Keys.S, OnSaveAs));
        file.DropDownItems.Add(Mi("모두 저장(&V)", Keys.None, OnSaveAll, Ico.SaveAll));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Mi("인쇄(&P)...", Keys.Control | Keys.P, OnPrint, Ico.Print));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Mi("끝내기(&X)", Keys.Alt | Keys.F4, OnExit));
        _menuStrip.Items.Add(file);

        // 편집
        var edit = new ToolStripMenuItem("편집(&E)");
        edit.DropDownItems.Add(Mi("실행 취소(&U)", Keys.Control | Keys.Z, OnUndo, Ico.Undo));
        edit.DropDownItems.Add(Mi("다시 실행(&R)", Keys.Control | Keys.Y, OnRedo, Ico.Redo));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Mi("잘라내기(&T)", Keys.Control | Keys.X, OnCut, Ico.Cut));
        edit.DropDownItems.Add(Mi("복사(&C)", Keys.Control | Keys.C, OnCopy, Ico.Copy));
        edit.DropDownItems.Add(Mi("붙여넣기(&P)", Keys.Control | Keys.V, OnPaste, Ico.Paste));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Mi("모두 선택(&A)", Keys.Control | Keys.A, OnSelectAll));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Mi("현재 줄 삭제(&D)", Keys.Control | Keys.Shift | Keys.K, OnDeleteLine));
        edit.DropDownItems.Add(Mi("현재 줄 복제(&L)", Keys.Control | Keys.Shift | Keys.D, OnDuplicateLine));
        edit.DropDownItems.Add(Mi("날짜/시간 삽입(&I)", Keys.F5, OnInsertDateTime));
        _menuStrip.Items.Add(edit);

        // 보기
        var view = new ToolStripMenuItem("보기(&V)");
        var toolbars = new ToolStripMenuItem("도구 모음(&T)");
        _miMainTool = new ToolStripMenuItem("표준 도구 모음") { CheckOnClick = true };
        _miFormatTool = new ToolStripMenuItem("서식 도구 모음") { CheckOnClick = true };
        _miMainTool.Click += (_, _) => { _showMainTool = _miMainTool.Checked; ApplyViewSettings(); };
        _miFormatTool.Click += (_, _) => { _showFormatTool = _miFormatTool.Checked; ApplyViewSettings(); };
        toolbars.DropDownItems.AddRange(new ToolStripItem[] { _miMainTool, _miFormatTool });
        view.DropDownItems.Add(toolbars);
        _miStatusBar = new ToolStripMenuItem("상태 표시줄(&S)") { CheckOnClick = true };
        _miStatusBar.Click += (_, _) => { _showStatusBar = _miStatusBar.Checked; ApplyViewSettings(); };
        view.DropDownItems.Add(_miStatusBar);
        _miDirPane = new ToolStripMenuItem("디렉토리 창(&D)") { CheckOnClick = true, ShortcutKeys = Keys.Alt | Keys.D1 };
        _miDirPane.Click += (_, _) => { _showDirPane = _miDirPane.Checked; ApplyViewSettings(); };
        view.DropDownItems.Add(_miDirPane);
        view.DropDownItems.Add(new ToolStripSeparator());
        _miRuler = new ToolStripMenuItem("눈금자(&R)", Img(Ico.Ruler)) { CheckOnClick = true };
        _miRuler.Click += (_, _) => { _showRuler = _miRuler.Checked; ApplyViewSettings(); };
        view.DropDownItems.Add(_miRuler);
        _miLineNum = new ToolStripMenuItem("줄 번호(&L)", Img(Ico.LineNumber)) { CheckOnClick = true };
        _miLineNum.Click += (_, _) => { _showLineNumbers = _miLineNum.Checked; ApplyViewSettings(); };
        view.DropDownItems.Add(_miLineNum);
        _miWrap = new ToolStripMenuItem("자동 줄 바꿈(&W)", Img(Ico.WordWrap)) { CheckOnClick = true, ShortcutKeys = Keys.Control | Keys.Shift | Keys.W };
        _miWrap.Click += OnToggleWordWrap;
        view.DropDownItems.Add(_miWrap);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add(Mi("편집창 글꼴 설정(&F)...", Keys.None, OnEditorFontSetting));
        _menuStrip.Items.Add(view);

        // 검색
        var search = new ToolStripMenuItem("검색(&S)");
        search.DropDownItems.Add(Mi("찾기(&F)...", Keys.Control | Keys.F, OnFind, Ico.Find));
        search.DropDownItems.Add(Mi("바꾸기(&R)...", Keys.Control | Keys.H, OnReplace, Ico.Replace));
        search.DropDownItems.Add(Mi("다음 찾기(&N)", Keys.F3, OnFindNext));
        search.DropDownItems.Add(Mi("이전 찾기(&P)", Keys.Shift | Keys.F3, OnFindPrev));
        search.DropDownItems.Add(new ToolStripSeparator());
        search.DropDownItems.Add(Mi("줄 이동(&G)...", Keys.Control | Keys.G, OnGoToLine, Ico.GoTo));
        search.DropDownItems.Add(new ToolStripSeparator());
        search.DropDownItems.Add(Mi("북마크 설정/해제(&B)", Keys.Control | Keys.F2, OnToggleBookmark, Ico.Bookmark));
        search.DropDownItems.Add(Mi("다음 북마크(&X)", Keys.F2, OnNextBookmark));
        search.DropDownItems.Add(Mi("이전 북마크(&V)", Keys.Shift | Keys.F2, OnPrevBookmark));
        search.DropDownItems.Add(Mi("모든 북마크 지우기(&C)", Keys.Control | Keys.Shift | Keys.F2, OnClearBookmarks));
        _menuStrip.Items.Add(search);

        // 문서
        _docMenu = new ToolStripMenuItem("문서(&D)");
        var encMenu = new ToolStripMenuItem("인코딩(&E) - 저장 형식");
        foreach (TextEnc enc in Enum.GetValues(typeof(TextEnc)))
        {
            var it = new ToolStripMenuItem(TextFileHelper.EncLabel(enc)) { Tag = enc };
            it.Click += (s, _) => SetEncoding((TextEnc)((ToolStripMenuItem)s!).Tag!);
            _encItems.Add(it);
            encMenu.DropDownItems.Add(it);
        }
        _docMenu.DropDownItems.Add(encMenu);
        var reopen = new ToolStripMenuItem("인코딩 지정하여 다시 열기(&O)");
        reopen.DropDownItems.Add(Mi("ANSI (CP949)", Keys.None, (_, _) => ReopenWithEncoding(TextEnc.Ansi)));
        reopen.DropDownItems.Add(Mi("UTF-8", Keys.None, (_, _) => ReopenWithEncoding(TextEnc.Utf8)));
        _docMenu.DropDownItems.Add(reopen);
        var eolMenu = new ToolStripMenuItem("줄 바꿈 형식(&F)");
        foreach (EolKind eol in Enum.GetValues(typeof(EolKind)))
        {
            string label = eol switch { EolKind.Pc => "PC (CR+LF)", EolKind.Unix => "UNIX (LF)", _ => "MAC (CR)" };
            var it = new ToolStripMenuItem(label) { Tag = eol };
            it.Click += (s, _) => SetEol((EolKind)((ToolStripMenuItem)s!).Tag!);
            _eolItems.Add(it);
            eolMenu.DropDownItems.Add(it);
        }
        _docMenu.DropDownItems.Add(eolMenu);
        _docMenu.DropDownItems.Add(new ToolStripSeparator());
        _docMenu.DropDownItems.Add(new ToolStripMenuItem("다음 문서(&N)", null, (_, _) => CycleDocument(+1)) { ShortcutKeyDisplayString = "Ctrl+Tab" });
        _docMenu.DropDownItems.Add(new ToolStripMenuItem("이전 문서(&P)", null, (_, _) => CycleDocument(-1)) { ShortcutKeyDisplayString = "Ctrl+Shift+Tab" });
        _docMenu.DropDownOpening += (_, _) => RefreshDocumentMenuState();
        _menuStrip.Items.Add(_docMenu);

        // 삽입
        var insert = new ToolStripMenuItem("삽입(&I)");
        insert.DropDownItems.Add(Mi("이미지 파일 삽입...", Keys.None, OnInsertImageFile, Ico.Image));
        insert.DropDownItems.Add(Mi("클립보드 이미지 붙여넣기", Keys.Control | Keys.Shift | Keys.V, OnPasteClipboardImage, Ico.ClipImage));
        insert.DropDownItems.Add(new ToolStripSeparator());
        insert.DropDownItems.Add(Mi("하이퍼링크 삽입...", Keys.Control | Keys.K, OnInsertLink, Ico.Link));
        insert.DropDownItems.Add(Mi("수평선 삽입", Keys.None, OnInsertHorizontalRule, Ico.HRule));
        insert.DropDownItems.Add(Mi("표 삽입...", Keys.None, OnInsertTable, Ico.Table));
        _menuStrip.Items.Add(insert);

        // 서식
        var format = new ToolStripMenuItem("서식(&O)");
        format.DropDownItems.Add(Mi("굵게(&B)", Keys.Control | Keys.B, OnBold, Ico.Bold));
        format.DropDownItems.Add(Mi("기울임(&I)", Keys.Control | Keys.I, OnItalic, Ico.Italic));
        format.DropDownItems.Add(Mi("밑줄(&U)", Keys.Control | Keys.U, OnUnderline, Ico.Underline));
        format.DropDownItems.Add(Mi("취소선", Keys.None, OnStrikethrough, Ico.Strike));
        format.DropDownItems.Add(new ToolStripSeparator());
        format.DropDownItems.Add(Mi("글꼴(&F)...", Keys.None, OnFont));
        format.DropDownItems.Add(Mi("글자 색상...", Keys.None, OnTextColor));
        format.DropDownItems.Add(Mi("배경 색상...", Keys.None, OnBackColor));
        _menuStrip.Items.Add(format);

        // 창
        _windowMenu = new ToolStripMenuItem("창(&W)");
        _windowMenu.DropDownItems.Add(new ToolStripMenuItem("(문서 없음)") { Enabled = false });
        _windowMenu.DropDownOpening += (_, _) => RebuildWindowMenu();
        _menuStrip.Items.Add(_windowMenu);

        // 도움말
        var help = new ToolStripMenuItem("도움말(&H)");
        help.DropDownItems.Add(Mi("정보(&A)", Keys.None, OnAbout));
        _menuStrip.Items.Add(help);
    }

    // ───────────────────────── 도구 모음 ─────────────────────────

    private ToolStripButton TB(string tip, Ico icon, EventHandler handler, Color? accent = null)
    {
        var b = new ToolStripButton
        {
            ToolTipText = tip,
            Image = Img(icon, accent),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ImageScaling = ToolStripItemImageScaling.None,
            Margin = new Padding(1, 1, 1, 1),
            Padding = new Padding(2)
        };
        b.Click += handler;
        return b;
    }

    private void BuildMainToolStrip()
    {
        var t = _mainToolStrip.Items;
        t.Add(TB("새로 만들기 (Ctrl+N)", Ico.New, OnNew));
        t.Add(TB("열기 (Ctrl+O)", Ico.Open, OnOpen));
        t.Add(TB("저장 (Ctrl+S)", Ico.Save, OnSave));
        t.Add(TB("모두 저장", Ico.SaveAll, OnSaveAll));
        t.Add(new ToolStripSeparator());
        t.Add(TB("인쇄 (Ctrl+P)", Ico.Print, OnPrint));
        t.Add(new ToolStripSeparator());
        t.Add(TB("잘라내기 (Ctrl+X)", Ico.Cut, OnCut));
        t.Add(TB("복사 (Ctrl+C)", Ico.Copy, OnCopy));
        t.Add(TB("붙여넣기 (Ctrl+V)", Ico.Paste, OnPaste));
        t.Add(new ToolStripSeparator());
        t.Add(TB("실행 취소 (Ctrl+Z)", Ico.Undo, OnUndo));
        t.Add(TB("다시 실행 (Ctrl+Y)", Ico.Redo, OnRedo));
        t.Add(new ToolStripSeparator());
        t.Add(TB("찾기 (Ctrl+F)", Ico.Find, OnFind));
        t.Add(TB("바꾸기 (Ctrl+H)", Ico.Replace, OnReplace));
        t.Add(TB("줄 이동 (Ctrl+G)", Ico.GoTo, OnGoToLine));
        t.Add(TB("북마크 설정/해제 (Ctrl+F2)", Ico.Bookmark, OnToggleBookmark));
        t.Add(new ToolStripSeparator());
        _wrapBtn = TB("자동 줄 바꿈 (Ctrl+Shift+W)", Ico.WordWrap, (_, _) => OnToggleWordWrap(null, EventArgs.Empty));
        _lineNumBtn = TB("줄 번호 표시", Ico.LineNumber, (_, _) => { _showLineNumbers = !_showLineNumbers; ApplyViewSettings(); });
        _rulerBtn = TB("눈금자 표시", Ico.Ruler, (_, _) => { _showRuler = !_showRuler; ApplyViewSettings(); });
        t.Add(_wrapBtn); t.Add(_lineNumBtn); t.Add(_rulerBtn);
        t.Add(new ToolStripSeparator());
        t.Add(TB("이미지 파일 삽입", Ico.Image, OnInsertImageFile));
        t.Add(TB("클립보드 이미지 붙여넣기 (Ctrl+Shift+V)", Ico.ClipImage, OnPasteClipboardImage));
    }

    private void BuildFormatToolStrip()
    {
        _fontFamilyCombo = new ToolStripComboBox("fontFamily")
        {
            DropDownStyle = ComboBoxStyle.DropDownList, ToolTipText = "글꼴", AutoSize = false,
            Size = new Size(S(180), S(23))
        };
        using (var ifc = new InstalledFontCollection())
            foreach (var family in ifc.Families) _fontFamilyCombo.Items.Add(family.Name);
        int idx = _fontFamilyCombo.Items.IndexOf(_editorFont.FontFamily.Name);
        _fontFamilyCombo.SelectedIndex = idx >= 0 ? idx : 0;
        _fontFamilyCombo.SelectedIndexChanged += FontFamily_Changed;
        _formatToolStrip.Items.Add(_fontFamilyCombo);

        _fontSizeCombo = new ToolStripComboBox("fontSize")
        {
            DropDownStyle = ComboBoxStyle.DropDown, ToolTipText = "글꼴 크기", AutoSize = false,
            Size = new Size(S(52), S(23))
        };
        _fontSizeCombo.Items.AddRange(new object[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32", "36", "48", "72" });
        _fontSizeCombo.Text = _editorFont.Size.ToString("0");
        _fontSizeCombo.KeyDown += FontSize_KeyDown;
        _fontSizeCombo.SelectedIndexChanged += FontSize_Changed;
        _formatToolStrip.Items.Add(_fontSizeCombo);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _paragraphCombo = new ToolStripComboBox("paragraph")
        {
            DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Size = new Size(S(72), S(23)), ToolTipText = "문단 서식"
        };
        _paragraphCombo.Items.AddRange(new object[] { "본문", "제목1", "제목2", "제목3" });
        _paragraphCombo.SelectedIndex = 0;
        _paragraphCombo.SelectedIndexChanged += Paragraph_Changed;
        _formatToolStrip.Items.Add(_paragraphCombo);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _boldBtn = TB("굵게 (Ctrl+B)", Ico.Bold, OnBold);
        _italicBtn = TB("기울임 (Ctrl+I)", Ico.Italic, OnItalic);
        _underlineBtn = TB("밑줄 (Ctrl+U)", Ico.Underline, OnUnderline);
        _strikeBtn = TB("취소선", Ico.Strike, OnStrikethrough);
        _formatToolStrip.Items.AddRange(new ToolStripItem[] { _boldBtn, _italicBtn, _underlineBtn, _strikeBtn, new ToolStripSeparator() });

        _textColorBtn = TB("글자 색상", Ico.TextColor, OnTextColor, _currentTextColor);
        _bgColorBtn = TB("배경 색상 (형광펜)", Ico.BackColor, OnBackColor, _currentBgColor);
        _formatToolStrip.Items.AddRange(new ToolStripItem[] { _textColorBtn, _bgColorBtn, new ToolStripSeparator() });

        _alignLeftBtn = TB("왼쪽 정렬", Ico.AlignLeft, OnAlignLeft);
        _alignCenterBtn = TB("가운데 정렬", Ico.AlignCenter, OnAlignCenter);
        _alignRightBtn = TB("오른쪽 정렬", Ico.AlignRight, OnAlignRight);
        _formatToolStrip.Items.AddRange(new ToolStripItem[] { _alignLeftBtn, _alignCenterBtn, _alignRightBtn, new ToolStripSeparator() });

        _bulletListBtn = TB("글머리 기호", Ico.Bullet, OnBulletList);
        _outdentBtn = TB("내어쓰기", Ico.Outdent, OnOutdent);
        _indentBtn = TB("들여쓰기", Ico.Indent, OnIndent);
        _formatToolStrip.Items.AddRange(new ToolStripItem[] { _bulletListBtn, _outdentBtn, _indentBtn, new ToolStripSeparator() });

        _formatToolStrip.Items.Add(TB("링크 삽입 (Ctrl+K)", Ico.Link, OnInsertLink));
        _formatToolStrip.Items.Add(TB("수평선", Ico.HRule, OnInsertHorizontalRule));
        _formatToolStrip.Items.Add(TB("표 삽입", Ico.Table, OnInsertTable));
    }

    /// <summary>글자/배경 색 선택 후 아이콘 아래 색 막대를 갱신합니다.</summary>
    private void UpdateColorIcons()
    {
        var old1 = _textColorBtn.Image; var old2 = _bgColorBtn.Image;
        _textColorBtn.Image = Img(Ico.TextColor, _currentTextColor);
        _bgColorBtn.Image = Img(Ico.BackColor, _currentBgColor);
        old1?.Dispose(); old2?.Dispose();
    }

    // ───────────────────────── 상태 표시줄 ─────────────────────────

    private ToolStripStatusLabel Pane(int width, ContentAlignment align = ContentAlignment.MiddleLeft) => new ToolStripStatusLabel
    {
        AutoSize = false,
        Width = S(width),
        TextAlign = align,
        BorderSides = ToolStripStatusLabelBorderSides.All,
        BorderStyle = Border3DStyle.SunkenOuter,
        Margin = new Padding(0, 2, 1, 0)
    };

    private void BuildStatusStrip()
    {
        _statusStrip = new StatusStrip { Dock = DockStyle.Fill, Font = _uiFont, SizingGrip = true, Margin = Padding.Empty };
        _statusLabel = new ToolStripStatusLabel("준비")
        {
            Spring = true, TextAlign = ContentAlignment.MiddleLeft,
            BorderSides = ToolStripStatusLabelBorderSides.All, BorderStyle = Border3DStyle.SunkenOuter,
            Margin = new Padding(0, 2, 1, 0)
        };
        _wordCountLabel = Pane(150, ContentAlignment.MiddleRight);
        _lineColLabel = Pane(170);
        _eolLabel = Pane(52, ContentAlignment.MiddleCenter);
        _encodingLabel = Pane(84, ContentAlignment.MiddleCenter);
        _wordCountLabel.Text = "0 단어 | 0 자";
        _lineColLabel.Text = "줄 1  칸 1  00";
        _eolLabel.Text = "PC";
        _encodingLabel.Text = "UTF-8 BOM";
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _wordCountLabel, _lineColLabel, _eolLabel, _encodingLabel });
    }

    // ───────────────────────── 왼쪽: 디렉토리 창 / 클립텍스트 ─────────────────────────

    private void BuildLeftPane()
    {
        _treeImages = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(IconSize, IconSize) };
        _treeImages.Images.Add(ToolIcons.Create(Ico.Folder, IconSize));
        _treeImages.Images.Add(ToolIcons.Create(Ico.FolderOpen, IconSize));

        _fileImages = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(IconSize, IconSize) };
        _fileImages.Images.Add(ToolIcons.Create(Ico.Page, IconSize));                                    // 0 기타
        _fileImages.Images.Add(ToolIcons.Create(Ico.Page, IconSize, Color.FromArgb(60, 120, 210)));      // 1 rtf
        _fileImages.Images.Add(ToolIcons.Create(Ico.Page, IconSize, Color.FromArgb(230, 120, 40)));      // 2 html

        // 드라이브 콤보
        _driveCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0) };
        foreach (var drive in DriveInfo.GetDrives())
            if (drive.IsReady) _driveCombo.Items.Add(drive.Name);
        if (_driveCombo.Items.Count > 0) _driveCombo.SelectedIndex = 0;
        _driveCombo.SelectedIndexChanged += DriveCombo_Changed;

        // 폴더 트리
        _folderTree = new TreeView
        {
            Dock = DockStyle.Fill, ShowLines = true, ShowPlusMinus = true, ShowRootLines = true, HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle, ItemHeight = S(18), Indent = S(16), ImageList = _treeImages,
            BackColor = Color.White
        };
        _folderTree.BeforeExpand += FolderTree_BeforeExpand;
        _folderTree.AfterSelect += FolderTree_AfterSelect;

        // 파일 목록
        _fileList = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None, FullRowSelect = true,
            MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, SmallImageList = _fileImages,
            BackColor = Color.White
        };
        _fileList.Columns.Add("이름", S(180));
        _fileList.Resize += (_, _) => { if (_fileList.Columns.Count > 0) _fileList.Columns[0].Width = Math.Max(S(50), _fileList.ClientSize.Width); };
        _fileList.DoubleClick += FileList_DoubleClick;
        _fileList.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { FileList_DoubleClick(null, EventArgs.Empty); e.Handled = true; } };

        // 필터 콤보
        _filterCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0) };
        _filterCombo.Items.AddRange(new object[] { "All Files (*.*)", "텍스트 (*.txt)", "RTF (*.rtf)", "HTML (*.html;*.htm)" });
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (_, _) => RefreshFileList();

        var innerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = S(4),
            BorderStyle = BorderStyle.None, BackColor = SystemColors.Control, SplitterDistance = S(230)
        };
        innerSplit.Panel1.Controls.Add(_folderTree);
        innerSplit.Panel2.Controls.Add(_fileList);

        var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        browser.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        browser.Controls.Add(_driveCombo, 0, 0);
        browser.Controls.Add(innerSplit, 0, 1);
        browser.Controls.Add(_filterCombo, 0, 2);

        // 클립텍스트
        _clipList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, DisplayMember = "" };
        _clipList.DoubleClick += (_, _) => InsertSelectedClip();
        _clipList.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { InsertSelectedClip(); e.Handled = true; } };
        var clipMenu = new ContextMenuStrip();
        clipMenu.Items.Add("삽입", null, (_, _) => InsertSelectedClip());
        clipMenu.Items.Add("선택 영역을 클립텍스트로 추가", null, (_, _) => AddSelectionToClip());
        clipMenu.Items.Add("삭제", null, (_, _) => RemoveSelectedClip());
        _clipList.ContextMenuStrip = clipMenu;

        _leftTabs = new TabControl { Dock = DockStyle.Fill, Alignment = TabAlignment.Bottom, Margin = Padding.Empty };
        var pageDir = new TabPage("디렉토리") { Padding = Padding.Empty, UseVisualStyleBackColor = true };
        var pageClip = new TabPage("클립텍스트") { Padding = Padding.Empty, UseVisualStyleBackColor = true };
        pageDir.Controls.Add(browser);
        pageClip.Controls.Add(_clipList);
        _leftTabs.TabPages.Add(pageDir);
        _leftTabs.TabPages.Add(pageClip);

        _splitContainer.Panel1.Controls.Add(_leftTabs);

        LoadDriveTree();
        RebuildClipList();
    }

    // ───────────────────────── 오른쪽: 문서 탭 + 눈금자 + 편집창 ─────────────────────────

    private void BuildEditorArea()
    {
        _docTabs = new DocumentTabStrip { Dock = DockStyle.Fill, Height = S(25), Margin = Padding.Empty };
        _docTabs.SelectedIndexChanged += DocTabs_SelectedIndexChanged;
        _docTabs.TabCloseRequested += (_, i) => CloseDocument(i);
        _docTabs.TabMoved += DocTabs_TabMoved;
        _docTabs.TabContextRequested += DocTabs_ContextRequested;
        _docTabs.DragEnter += Files_DragEnter;
        _docTabs.DragDrop += Files_DragDrop;

        _ruler = new RulerPanel { Dock = DockStyle.Fill, Height = S(18), Margin = Padding.Empty };

        _editorHost = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.Fixed3D, BackColor = Color.White, Margin = Padding.Empty };
        _lineNumberPanel = new LineNumberPanel { Dock = DockStyle.Left, Font = _editorFont };
        _editorHost.Controls.Add(_lineNumberPanel);
        _lineNumberPanel.SizeChanged += (_, _) => SyncRuler();
        _lineNumberPanel.VisibleChanged += (_, _) => SyncRuler();
        _editorHost.SizeChanged += (_, _) => SyncRuler();

        var area = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        area.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        area.Controls.Add(_docTabs, 0, 0);
        area.Controls.Add(_ruler, 0, 1);
        area.Controls.Add(_editorHost, 0, 2);
        _splitContainer.Panel2.Controls.Add(area);
    }

    /// <summary>눈금자의 시작 위치를 줄 번호 거터 폭에 맞춥니다.</summary>
    private void SyncRuler()
    {
        if (_ruler == null || _editorHost == null) return;
        int border = (_editorHost.Width - _editorHost.ClientSize.Width) / 2;
        _ruler.LeftOffset = _editorHost.Left + border + (_lineNumberPanel.Visible ? _lineNumberPanel.Width : 0);
    }
}
