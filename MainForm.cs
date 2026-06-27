using System.Text;
using System.Drawing.Text;

namespace YunseulMemo;

public partial class MainForm : Form
{

    private string _appTitle = "윤슬 에디터 v1.1";

    // Layout
    private MenuStrip _menuStrip = null!;
    private ToolStrip _mainToolStrip = null!;
    private ToolStrip _formatToolStrip = null!;
    private SplitContainer _splitContainer = null!;
    private Panel _editorPanel = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripStatusLabel _wordCountLabel = null!;
    private ToolStripStatusLabel _lineColLabel = null!;
    private ToolStripStatusLabel _encodingLabel = null!;

    // File Browser (left panel)
    private ComboBox _driveCombo = null!;
    private TreeView _folderTree = null!;
    private ListBox _fileList = null!;
    private ComboBox _filterCombo = null!;

    // Tab bar (bottom of editor area, above status bar)
    private TabControl _tabBar = null!;

    // Find/Replace
    private Panel _findPanel = null!;
    private TextBox _findTextBox = null!;
    private TextBox _replaceTextBox = null!;

    // Format toolbar controls
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

    private Color _currentTextColor = Color.Black;
    private Color _currentBgColor = Color.Yellow;

    private const RichTextBoxFinds FindOptions = RichTextBoxFinds.None;

    // Line number panel
    private LineNumberPanel _lineNumberPanel = null!;

    // Multi-document support
    private List<DocumentTab> _documents = new();
    private int _activeDocIndex = -1;
    private int _untitledCounter = 0;

    private string _currentBrowsePath = "";
    private string _settingsPath = "";

    // A4 page outline panel
    private Panel _pageOutlinePanel = null!;

    public MainForm()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YunseulMemo", "settings.ini");
        InitializeComponent();
        LoadSettings();
        // Start with one empty document
        NewDocument();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = _appTitle;
        Size = new Size(1100, 750);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;

        // Menu
        _menuStrip = new MenuStrip();
        BuildMenu();
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;

        // Main Toolbar
        _mainToolStrip = new ToolStrip
        {
            ImageScalingSize = new Size(24, 24),
            Font = new Font("Segoe UI Emoji", 14f),
            AutoSize = true
        };
        BuildMainToolStrip();
        Controls.Add(_mainToolStrip);

        // Format Toolbar
        _formatToolStrip = new ToolStrip
        {
            ImageScalingSize = new Size(24, 24),
            Font = new Font("맑은 고딕", 11f),
            AutoSize = true
        };
        BuildFormatToolStrip();
        Controls.Add(_formatToolStrip);

        // Find Panel
        BuildFindPanel();
        Controls.Add(_findPanel);

        // Status Strip
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("준비") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        _wordCountLabel = new ToolStripStatusLabel("0 단어 | 0 자");
        _lineColLabel = new ToolStripStatusLabel("줄: 1, 열: 1");
        _encodingLabel = new ToolStripStatusLabel("UTF-8");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _wordCountLabel, _lineColLabel, _encodingLabel });
        Controls.Add(_statusStrip);

        // Main split: left = file browser, right = editor + tabs
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 180,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(220, 220, 215)
        };
        _splitContainer.Paint += SplitContainer_Paint;

        // Left panel: file browser
        BuildFileBrowser();

        // Right panel: tab bar at bottom + editor area
        _editorPanel = new Panel { Dock = DockStyle.Fill };

        // A4 page outline panel (contains the editor with page-like appearance)
        _pageOutlinePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 245, 242), // 배경 (용지 바깥)
            // A4 여백 가이드: 상하좌우에 약간의 패딩으로 용지 느낌
            Padding = new Padding(0, 12, 20, 12)
        };
        _pageOutlinePanel.Paint += PageOutlinePanel_Paint;

        // Line number panel (left side of editor) - 편집창과 자연스럽게 연결
        _lineNumberPanel = new LineNumberPanel
        {
            Dock = DockStyle.Left,
            Width = 52
        };
        _pageOutlinePanel.Controls.Add(_lineNumberPanel);

        _editorPanel.Controls.Add(_pageOutlinePanel);
        _pageOutlinePanel.BringToFront();

        _tabBar = new TabControl
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Alignment = TabAlignment.Bottom,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(150, 22),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Font = new Font("맑은 고딕", 9f),
            Padding = new Point(12, 3)
        };
        _tabBar.DrawItem += TabBar_DrawItem;
        _tabBar.SelectedIndexChanged += TabBar_SelectedIndexChanged;
        _tabBar.MouseDown += TabBar_MouseDown;

        _editorPanel.Controls.Add(_tabBar);
        _splitContainer.Panel2.Controls.Add(_editorPanel);

        Controls.Add(_splitContainer);
        _splitContainer.BringToFront();

        ResumeLayout(false);
        PerformLayout();
    }

    #region File Browser

    private void BuildFileBrowser()
    {
        var browserPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // Drive selector
        _driveCombo = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 24,
            Font = new Font("맑은 고딕", 9f),
            FlatStyle = FlatStyle.Flat
        };
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
                _driveCombo.Items.Add(drive.Name);
        }
        if (_driveCombo.Items.Count > 0) _driveCombo.SelectedIndex = 0;
        _driveCombo.SelectedIndexChanged += DriveCombo_Changed;

        // Folder tree (top half)
        _folderTree = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("맑은 고딕", 9.5f),
            ItemHeight = 20,
            BackColor = Color.White
        };
        _folderTree.BeforeExpand += FolderTree_BeforeExpand;
        _folderTree.AfterSelect += FolderTree_AfterSelect;

        // File filter
        _filterCombo = new ComboBox
        {
            Dock = DockStyle.Bottom,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 24,
            Font = new Font("맑은 고딕", 9f),
            FlatStyle = FlatStyle.Flat
        };
        _filterCombo.Items.AddRange(new object[] { "All Files (*.*)", "텍스트 (*.txt)", "RTF (*.rtf)", "HTML (*.html;*.htm)" });
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (s, e) => RefreshFileList();

        // File list (bottom half)
        _fileList = new ListBox
        {
            Dock = DockStyle.Bottom,
            Height = 200,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("맑은 고딕", 9.5f),
            BackColor = Color.White
        };
        _fileList.DoubleClick += FileList_DoubleClick;

        // Split folder tree and file list
        var innerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 250,
            SplitterWidth = 4,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(230, 230, 225)
        };
        innerSplit.Panel1.Controls.Add(_folderTree);
        innerSplit.Panel2.Controls.Add(_fileList);
        innerSplit.Panel2.Controls.Add(_filterCombo);

        browserPanel.Controls.Add(innerSplit);
        browserPanel.Controls.Add(_driveCombo);
        _driveCombo.BringToFront();

        _splitContainer.Panel1.Controls.Add(browserPanel);

        // Initialize tree
        LoadDriveTree();
    }

    private void LoadDriveTree()
    {
        _folderTree.Nodes.Clear();
        if (_driveCombo.SelectedItem == null) return;

        string drivePath = _driveCombo.SelectedItem.ToString()!;
        var rootNode = new TreeNode(drivePath) { Tag = drivePath };
        rootNode.Nodes.Add(""); // placeholder
        _folderTree.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private void DriveCombo_Changed(object? sender, EventArgs e)
    {
        LoadDriveTree();
    }

    private void FolderTree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node == null) return;
        var node = e.Node;
        if (node.Nodes.Count == 1 && node.Nodes[0].Text == "")
        {
            node.Nodes.Clear();
            string path = node.Tag?.ToString() ?? "";
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var child = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                    child.Nodes.Add(""); // placeholder
                    node.Nodes.Add(child);
                }
            }
            catch { }
        }
    }

    private void FolderTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag == null) return;
        _currentBrowsePath = e.Node.Tag.ToString()!;
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        _fileList.Items.Clear();
        if (string.IsNullOrEmpty(_currentBrowsePath)) return;

        string pattern = _filterCombo.SelectedIndex switch
        {
            1 => "*.txt",
            2 => "*.rtf",
            3 => "*.htm*",
            _ => "*.*"
        };

        try
        {
            foreach (var file in Directory.GetFiles(_currentBrowsePath, pattern))
            {
                _fileList.Items.Add(Path.GetFileName(file));
            }
        }
        catch { }
    }

    private void FileList_DoubleClick(object? sender, EventArgs e)
    {
        if (_fileList.SelectedItem == null) return;
        string fileName = _fileList.SelectedItem.ToString()!;
        string fullPath = Path.Combine(_currentBrowsePath, fileName);
        OpenFileInNewTab(fullPath);
    }

    #endregion

    #region Multi-Document / Tab Management

    private DocumentTab ActiveDoc => _activeDocIndex >= 0 && _activeDocIndex < _documents.Count
        ? _documents[_activeDocIndex] : null!;

    private RichTextBox? ActiveEditor => ActiveDoc?.Editor;

    private void NewDocument()
    {
        _untitledCounter++;
        var doc = new DocumentTab
        {
            Title = $"제목 없음{_untitledCounter}",
            FilePath = null,
            IsModified = false,
            Editor = CreateEditor()
        };
        _documents.Add(doc);
        var tab = new TabPage(doc.Title);
        _tabBar.TabPages.Add(tab);
        SwitchToDocument(_documents.Count - 1);
    }

    private void OpenFileInNewTab(string path)
    {
        // 이미 열려있는지 확인
        for (int i = 0; i < _documents.Count; i++)
        {
            if (_documents[i].FilePath != null &&
                string.Equals(_documents[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                SwitchToDocument(i);
                return;
            }
        }

        // 대용량 파일 판별 (5MB 이상 텍스트 파일)
        const long LargeFileThreshold = 5 * 1024 * 1024;
        var fileInfo = new FileInfo(path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        bool isTextFile = ext is ".txt" or ".log" or ".csv" or ".tsv" or ".sql" or ".json" or ".xml" or ".md" or "";
        bool useLargeMode = fileInfo.Length > LargeFileThreshold && isTextFile;

        if (useLargeMode)
        {
            OpenLargeFile(path);
            return;
        }

        var doc = new DocumentTab
        {
            Title = Path.GetFileName(path),
            FilePath = path,
            IsModified = false,
            Editor = CreateEditor()
        };

        try
        {
            switch (ext)
            {
                case ".rtf":
                    doc.Editor.LoadFile(path, RichTextBoxStreamType.RichText);
                    break;
                case ".ymemo":
                    // 하위 호환: 기존 ymemo 파일도 열 수 있음
                    LoadYmemoInto(doc.Editor, path);
                    break;
                default:
                    doc.Editor.Text = File.ReadAllText(path, Encoding.UTF8);
                    break;
            }
            doc.IsModified = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 열 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _documents.Add(doc);
        var tab = new TabPage(doc.Title);
        _tabBar.TabPages.Add(tab);
        SwitchToDocument(_documents.Count - 1);
        _statusLabel.Text = $"열림: {path}";
    }

    private void OpenLargeFile(string path)
    {
        var viewer = new LargeFileViewer
        {
            Dock = DockStyle.Fill,
            Visible = false
        };

        try
        {
            viewer.LoadFile(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"대용량 파일을 열 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            viewer.Dispose();
            return;
        }

        var doc = new DocumentTab
        {
            Title = Path.GetFileName(path),
            FilePath = path,
            IsModified = false,
            LargeViewer = viewer
        };

        _documents.Add(doc);
        var tab = new TabPage(doc.Title);
        _tabBar.TabPages.Add(tab);
        SwitchToDocument(_documents.Count - 1);

        long sizeMB = new FileInfo(path).Length / (1024 * 1024);
        _statusLabel.Text = $"대용량 모드로 열림: {path} ({sizeMB}MB, {viewer.TotalLines:N0}줄)";
    }

    private void SwitchToDocument(int index)
    {
        if (index < 0 || index >= _documents.Count) return;

        // 이전 문서 숨기기
        if (_activeDocIndex >= 0 && _activeDocIndex < _documents.Count)
        {
            var prev = _documents[_activeDocIndex];
            if (prev.IsLargeFile)
            {
                if (prev.LargeViewer?.Parent != null)
                    prev.LargeViewer.Visible = false;
            }
            else if (prev.Editor != null)
            {
                if (prev.Editor.Parent != null)
                    prev.Editor.Visible = false;
            }
        }

        _activeDocIndex = index;
        var doc = _documents[index];

        if (doc.IsLargeFile)
        {
            // 대용량 뷰어 표시
            if (doc.LargeViewer!.Parent == null)
            {
                doc.LargeViewer.Dock = DockStyle.Fill;
                _pageOutlinePanel.Controls.Add(doc.LargeViewer);
                doc.LargeViewer.BringToFront();
            }
            doc.LargeViewer.Visible = true;
            doc.LargeViewer.BringToFront();
            doc.LargeViewer.Focus();
            _lineNumberPanel.DetachEditor();
            _lineNumberPanel.Visible = false;
        }
        else if (doc.Editor != null)
        {
            if (doc.Editor.Parent == null)
            {
                doc.Editor.Dock = DockStyle.Fill;
                _pageOutlinePanel.Controls.Add(doc.Editor);
                doc.Editor.BringToFront();
            }
            doc.Editor.Visible = true;
            doc.Editor.BringToFront();
            doc.Editor.Focus();
            _lineNumberPanel.Visible = true;
            _lineNumberPanel.AttachEditor(doc.Editor);
        }

        // 탭 선택 동기화
        if (_tabBar.SelectedIndex != index)
            _tabBar.SelectedIndex = index;

        UpdateTitle();
        UpdateWordCount();
        UpdateCursorPosition();
    }

    private void CloseDocument(int index)
    {
        if (index < 0 || index >= _documents.Count) return;

        var doc = _documents[index];
        if (doc.IsModified)
        {
            var result = MessageBox.Show(
                $"'{doc.Title}'의 변경 내용을 저장하시겠습니까?",
                _appTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                _activeDocIndex = index;
                OnSave(null, EventArgs.Empty);
                if (doc.IsModified) return; // save was cancelled
            }
        }

        // 에디터/뷰어 제거
        if (doc.IsLargeFile)
        {
            if (doc.LargeViewer?.Parent != null)
                _pageOutlinePanel.Controls.Remove(doc.LargeViewer);
            doc.LargeViewer?.Dispose();
        }
        else if (doc.Editor != null)
        {
            if (doc.Editor.Parent != null)
                _pageOutlinePanel.Controls.Remove(doc.Editor);
            doc.Editor.Dispose();
        }

        _documents.RemoveAt(index);
        _tabBar.TabPages.RemoveAt(index);

        if (_documents.Count == 0)
        {
            NewDocument();
        }
        else
        {
            int newIndex = Math.Min(index, _documents.Count - 1);
            SwitchToDocument(newIndex);
        }
    }

    private RichTextBox CreateEditor()
    {
        var rtb = new ResizableRichTextBox
        {
            Dock = DockStyle.Fill,
            AcceptsTab = true,
            Font = new Font("맑은 고딕", 12f),
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = true,
            EnableAutoDragDrop = true,
            Visible = false,
            AllowDrop = true,
            BorderStyle = BorderStyle.None
        };
        rtb.Margin = new Padding(0, 0, 10, 0);
        rtb.TextChanged += Editor_TextChanged;
        rtb.SelectionChanged += Editor_SelectionChanged;
        rtb.KeyDown += Editor_KeyDown;

        // Ctrl+V (WM_PASTE)를 가로채서 이미지+텍스트 혼합 붙여넣기 지원
        rtb.CustomPasteHandler = () => HandleCustomPaste(rtb);

        return rtb;
    }

    /// <summary>
    /// WM_PASTE를 가로채서 RTF/이미지 포함 붙여넣기를 처리합니다.
    /// true 반환 시 기본 paste를 차단합니다.
    /// </summary>
    private bool HandleCustomPaste(RichTextBox editor)
    {
        if (Clipboard.ContainsData(DataFormats.Rtf))
        {
            editor.Paste(DataFormats.GetFormat(DataFormats.Rtf));
            return true;
        }
        else if (Clipboard.ContainsImage())
        {
            using var img = Clipboard.GetImage();
            if (img != null)
            {
                InsertImageAtOriginalSize(img);
                _statusLabel.Text = $"클립보드 이미지 삽입됨 ({img.Width}×{img.Height})";
            }
            return true;
        }
        // HTML이나 텍스트는 기본 paste에 맡김
        return false;
    }

    private void UpdateTabTitle(int index)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];
        string title = doc.FilePath != null ? Path.GetFileName(doc.FilePath) : doc.Title;
        string prefix = doc.IsModified ? "*" : "";
        _tabBar.TabPages[index].Text = $"{prefix}{title}";
    }

    private void TabBar_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabBar.SelectedIndex >= 0 && _tabBar.SelectedIndex != _activeDocIndex)
        {
            SwitchToDocument(_tabBar.SelectedIndex);
        }
    }

    private void TabBar_MouseDown(object? sender, MouseEventArgs e)
    {
        for (int i = 0; i < _tabBar.TabCount; i++)
        {
            var tabRect = _tabBar.GetTabRect(i);

            // Close button area: 14x14 box at the right side of the tab
            var closeRect = new Rectangle(
                tabRect.Right - 18, tabRect.Top + (tabRect.Height - 14) / 2, 14, 14);

            if (closeRect.Contains(e.Location) && e.Button == MouseButtons.Left)
            {
                CloseDocument(i);
                return;
            }
        }

        // Middle-click to close tab
        if (e.Button == MouseButtons.Middle)
        {
            for (int i = 0; i < _tabBar.TabCount; i++)
            {
                if (_tabBar.GetTabRect(i).Contains(e.Location))
                {
                    CloseDocument(i);
                    break;
                }
            }
        }
    }

    private void TabBar_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabBar.TabCount) return;

        var tabRect = _tabBar.GetTabRect(e.Index);
        bool isSelected = (_tabBar.SelectedIndex == e.Index);

        // Background
        using var bgBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(240, 240, 238));
        e.Graphics.FillRectangle(bgBrush, tabRect);

        // Tab text
        string tabText = _tabBar.TabPages[e.Index].Text;
        using var textBrush = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(80, 80, 80));
        using var textFont = new Font("맑은 고딕", 9f, isSelected ? FontStyle.Bold : FontStyle.Regular);
        var textRect = new Rectangle(tabRect.Left + 6, tabRect.Top + 3, tabRect.Width - 26, tabRect.Height - 4);
        using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            e.Graphics.DrawString(tabText, textFont, textBrush, textRect, sf);

        // Close button (X)
        var closeRect = new Rectangle(tabRect.Right - 18, tabRect.Top + (tabRect.Height - 14) / 2, 14, 14);
        using var closeBrush = new SolidBrush(Color.FromArgb(120, 120, 120));
        using var closeFont = new Font("맑은 고딕", 8f, FontStyle.Bold);
        e.Graphics.DrawString("✕", closeFont, closeBrush, closeRect.Left, closeRect.Top);

        // Bottom border for unselected tabs
        if (!isSelected)
        {
            using var borderPen = new Pen(Color.FromArgb(200, 200, 195));
            e.Graphics.DrawLine(borderPen, tabRect.Left, tabRect.Bottom - 1, tabRect.Right, tabRect.Bottom - 1);
        }
    }

    #endregion

    #region Menu

    private void BuildMenu()
    {
        var fileMenu = new ToolStripMenuItem("파일(&F)");
        fileMenu.DropDownItems.Add(CreateMenuItem("새로 만들기(&N)", Keys.Control | Keys.N, OnNew));
        fileMenu.DropDownItems.Add(CreateMenuItem("열기(&O)...", Keys.Control | Keys.O, OnOpen));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(CreateMenuItem("저장(&S)", Keys.Control | Keys.S, OnSave));
        fileMenu.DropDownItems.Add(CreateMenuItem("다른 이름으로 저장(&A)...", Keys.Control | Keys.Shift | Keys.S, OnSaveAs));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(CreateMenuItem("닫기(&W)", Keys.Control | Keys.W, OnCloseTab));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(CreateMenuItem("인쇄(&P)...", Keys.Control | Keys.P, OnPrint));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(CreateMenuItem("끝내기(&X)", Keys.Alt | Keys.F4, OnExit));
        _menuStrip.Items.Add(fileMenu);

        var editMenu = new ToolStripMenuItem("편집(&E)");
        editMenu.DropDownItems.Add(CreateMenuItem("실행 취소(&U)", Keys.Control | Keys.Z, OnUndo));
        editMenu.DropDownItems.Add(CreateMenuItem("다시 실행(&R)", Keys.Control | Keys.Y, OnRedo));
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(CreateMenuItem("잘라내기(&T)", Keys.Control | Keys.X, OnCut));
        editMenu.DropDownItems.Add(CreateMenuItem("복사(&C)", Keys.Control | Keys.C, OnCopy));
        editMenu.DropDownItems.Add(CreateMenuItem("붙여넣기(&P)", Keys.Control | Keys.V, OnPaste));
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(CreateMenuItem("모두 선택(&A)", Keys.Control | Keys.A, OnSelectAll));
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(CreateMenuItem("찾기/바꾸기(&F)...", Keys.Control | Keys.H, OnToggleFind));
        _menuStrip.Items.Add(editMenu);

        var insertMenu = new ToolStripMenuItem("삽입(&I)");
        insertMenu.DropDownItems.Add(CreateMenuItem("이미지 파일 삽입...", Keys.None, OnInsertImageFile));
        insertMenu.DropDownItems.Add(CreateMenuItem("클립보드 이미지 붙여넣기", Keys.Control | Keys.Shift | Keys.V, OnPasteClipboardImage));
        insertMenu.DropDownItems.Add(new ToolStripSeparator());
        insertMenu.DropDownItems.Add(CreateMenuItem("하이퍼링크 삽입...", Keys.Control | Keys.K, OnInsertLink));
        insertMenu.DropDownItems.Add(CreateMenuItem("수평선 삽입", Keys.None, OnInsertHorizontalRule));
        insertMenu.DropDownItems.Add(CreateMenuItem("표 삽입...", Keys.None, OnInsertTable));
        _menuStrip.Items.Add(insertMenu);

        var formatMenu = new ToolStripMenuItem("서식(&O)");
        formatMenu.DropDownItems.Add(CreateMenuItem("굵게(&B)", Keys.Control | Keys.B, OnBold));
        formatMenu.DropDownItems.Add(CreateMenuItem("기울임(&I)", Keys.Control | Keys.I, OnItalic));
        formatMenu.DropDownItems.Add(CreateMenuItem("밑줄(&U)", Keys.Control | Keys.U, OnUnderline));
        formatMenu.DropDownItems.Add(CreateMenuItem("취소선", Keys.None, OnStrikethrough));
        formatMenu.DropDownItems.Add(new ToolStripSeparator());
        formatMenu.DropDownItems.Add(CreateMenuItem("글꼴(&F)...", Keys.None, OnFont));
        formatMenu.DropDownItems.Add(CreateMenuItem("글자 색상...", Keys.None, OnTextColor));
        formatMenu.DropDownItems.Add(CreateMenuItem("배경 색상...", Keys.None, OnBackColor));
        formatMenu.DropDownItems.Add(new ToolStripSeparator());
        formatMenu.DropDownItems.Add(CreateMenuItem("자동 줄 바꿈(&W)", Keys.None, OnToggleWordWrap));
        _menuStrip.Items.Add(formatMenu);

        var helpMenu = new ToolStripMenuItem("도움말(&H)");
        helpMenu.DropDownItems.Add(CreateMenuItem("정보(&A)", Keys.None, OnAbout));
        _menuStrip.Items.Add(helpMenu);
    }

    #endregion

    #region Toolbars

    private void BuildMainToolStrip()
    {
        _mainToolStrip.Items.Add(CreateToolButton("새로 만들기", "📄", OnNew));
        _mainToolStrip.Items.Add(CreateToolButton("열기", "📂", OnOpen));
        _mainToolStrip.Items.Add(CreateToolButton("저장", "💾", OnSave));
        _mainToolStrip.Items.Add(new ToolStripSeparator());
        _mainToolStrip.Items.Add(CreateToolButton("잘라내기", " ✂ ", OnCut));
        _mainToolStrip.Items.Add(CreateToolButton("복사", "📋", OnCopy));
        _mainToolStrip.Items.Add(CreateToolButton("붙여넣기", "📌", OnPaste));
        _mainToolStrip.Items.Add(new ToolStripSeparator());
        _mainToolStrip.Items.Add(CreateToolButton("실행 취소", " ↩ ", OnUndo));
        _mainToolStrip.Items.Add(CreateToolButton("다시 실행", " ↪ ", OnRedo));
        _mainToolStrip.Items.Add(new ToolStripSeparator());
        _mainToolStrip.Items.Add(CreateToolButton("이미지 삽입", "🖼", OnInsertImageFile));
        _mainToolStrip.Items.Add(CreateToolButton("클립보드 이미지", "📷", OnPasteClipboardImage));
        _mainToolStrip.Items.Add(new ToolStripSeparator());
        _mainToolStrip.Items.Add(CreateToolButton("찾기/바꾸기", "🔍", OnToggleFind));
        _mainToolStrip.Items.Add(new ToolStripSeparator());
        _mainToolStrip.Items.Add(CreateToolButton("인쇄", "🖨", OnPrint));
    }

    private void BuildFormatToolStrip()
    {
        _fontFamilyCombo = new ToolStripComboBox("fontFamily")
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, ToolTipText = "글꼴"
        };
        // 시스템에 설치된 모든 폰트 로딩
        using (var ifc = new InstalledFontCollection())
        {
            foreach (var family in ifc.Families)
            {
                _fontFamilyCombo.Items.Add(family.Name);
            }
        }
        // 기본 폰트 선택
        int defaultFontIdx = _fontFamilyCombo.Items.IndexOf("맑은 고딕");
        _fontFamilyCombo.SelectedIndex = defaultFontIdx >= 0 ? defaultFontIdx : 0;
        _fontFamilyCombo.SelectedIndexChanged += FontFamily_Changed;
        _formatToolStrip.Items.Add(_fontFamilyCombo);

        _fontSizeCombo = new ToolStripComboBox("fontSize")
        {
            DropDownStyle = ComboBoxStyle.DropDown, Width = 45, ToolTipText = "글꼴 크기"
        };
        _fontSizeCombo.Items.AddRange(new object[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32", "36", "48", "72" });
        _fontSizeCombo.Text = "12";
        _fontSizeCombo.KeyDown += FontSize_KeyDown;
        _fontSizeCombo.SelectedIndexChanged += FontSize_Changed;
        _formatToolStrip.Items.Add(_fontSizeCombo);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _paragraphCombo = new ToolStripComboBox("paragraph")
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 75, ToolTipText = "문단 서식"
        };
        _paragraphCombo.Items.AddRange(new object[] { "본문", "제목1", "제목2", "제목3" });
        _paragraphCombo.SelectedIndex = 0;
        _paragraphCombo.SelectedIndexChanged += Paragraph_Changed;
        _formatToolStrip.Items.Add(_paragraphCombo);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _boldBtn = CreateToolButton("굵게 (Ctrl+B)", " B ", OnBold);
        _boldBtn.Font = new Font("맑은 고딕", 12f, FontStyle.Bold);
        _formatToolStrip.Items.Add(_boldBtn);

        _italicBtn = CreateToolButton("기울임 (Ctrl+I)", " I ", OnItalic);
        _italicBtn.Font = new Font("맑은 고딕", 12f, FontStyle.Italic);
        _formatToolStrip.Items.Add(_italicBtn);

        _underlineBtn = CreateToolButton("밑줄 (Ctrl+U)", " U ", OnUnderline);
        _underlineBtn.Font = new Font("맑은 고딕", 12f, FontStyle.Underline);
        _formatToolStrip.Items.Add(_underlineBtn);

        _strikeBtn = CreateToolButton("취소선", " S ", OnStrikethrough);
        _strikeBtn.Font = new Font("맑은 고딕", 12f, FontStyle.Strikeout);
        _formatToolStrip.Items.Add(_strikeBtn);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _textColorBtn = CreateToolButton("글자 색상", " A ", OnTextColor);
        _textColorBtn.Font = new Font("맑은 고딕", 12f, FontStyle.Bold);
        _textColorBtn.ForeColor = _currentTextColor;
        _formatToolStrip.Items.Add(_textColorBtn);

        _bgColorBtn = CreateToolButton("배경 색상", " ▮ ", OnBackColor);
        _bgColorBtn.Font = new Font("맑은 고딕", 12f);
        _bgColorBtn.ForeColor = _currentBgColor;
        _formatToolStrip.Items.Add(_bgColorBtn);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _alignLeftBtn = CreateToolButton("왼쪽 정렬", "≡←", OnAlignLeft);
        _formatToolStrip.Items.Add(_alignLeftBtn);
        _alignCenterBtn = CreateToolButton("가운데 정렬", "≡↔", OnAlignCenter);
        _formatToolStrip.Items.Add(_alignCenterBtn);
        _alignRightBtn = CreateToolButton("오른쪽 정렬", "≡→", OnAlignRight);
        _formatToolStrip.Items.Add(_alignRightBtn);

        _formatToolStrip.Items.Add(new ToolStripSeparator());

        _bulletListBtn = CreateToolButton("글머리 기호", "•≡", OnBulletList);
        _formatToolStrip.Items.Add(_bulletListBtn);
        _outdentBtn = CreateToolButton("내어쓰기", "◁≡", OnOutdent);
        _formatToolStrip.Items.Add(_outdentBtn);
        _indentBtn = CreateToolButton("들여쓰기", "▷≡", OnIndent);
        _formatToolStrip.Items.Add(_indentBtn);

        _formatToolStrip.Items.Add(new ToolStripSeparator());
        _formatToolStrip.Items.Add(CreateToolButton("링크 삽입", "🔗", OnInsertLink));
        _formatToolStrip.Items.Add(CreateToolButton("수평선", "─", OnInsertHorizontalRule));
        _formatToolStrip.Items.Add(CreateToolButton("표 삽입", "⊞", OnInsertTable));
    }

    #endregion

    #region Find/Replace

    private void BuildFindPanel()
    {
        _findPanel = new Panel
        {
            Dock = DockStyle.Top, Height = 36, Visible = false,
            BackColor = SystemColors.Control, Padding = new Padding(8, 4, 8, 4)
        };

        var findLabel = new Label { Text = "찾기:", AutoSize = true, Location = new Point(8, 10) };
        _findTextBox = new TextBox { Location = new Point(50, 7), Width = 150 };
        _findTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { DoFind(); e.SuppressKeyPress = true; } };

        var replaceLabel = new Label { Text = "바꾸기:", AutoSize = true, Location = new Point(210, 10) };
        _replaceTextBox = new TextBox { Location = new Point(265, 7), Width = 150 };

        var findBtn = new Button { Text = "찾기", Location = new Point(425, 5), Width = 60, Height = 26 };
        findBtn.Click += (s, e) => DoFind();
        var replaceBtn = new Button { Text = "바꾸기", Location = new Point(490, 5), Width = 60, Height = 26 };
        replaceBtn.Click += (s, e) => DoReplace();
        var replaceAllBtn = new Button { Text = "모두 바꾸기", Location = new Point(555, 5), Width = 80, Height = 26 };
        replaceAllBtn.Click += (s, e) => DoReplaceAll();
        var closeBtn = new Button { Text = "✕", Location = new Point(645, 5), Width = 30, Height = 26, FlatStyle = FlatStyle.Flat };
        closeBtn.Click += (s, e) => { _findPanel.Visible = false; };

        _findPanel.Controls.AddRange(new Control[] { findLabel, _findTextBox, replaceLabel, _replaceTextBox, findBtn, replaceBtn, replaceAllBtn, closeBtn });
    }

    private void DoFind()
    {
        if (ActiveEditor == null) return;
        string searchText = _findTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        int startIndex = ActiveEditor.SelectionStart + ActiveEditor.SelectionLength;
        int index = ActiveEditor.Find(searchText, startIndex, FindOptions);
        if (index == -1) index = ActiveEditor.Find(searchText, 0, FindOptions);

        if (index == -1)
            _statusLabel.Text = "찾을 수 없습니다.";
        else
        {
            ActiveEditor.Select(index, searchText.Length);
            ActiveEditor.ScrollToCaret();
            _statusLabel.Text = $"찾음: 위치 {index}";
        }
    }

    private void DoReplace()
    {
        if (ActiveEditor == null) return;
        string searchText = _findTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;
        if (string.Equals(ActiveEditor.SelectedText, searchText, StringComparison.OrdinalIgnoreCase))
            ActiveEditor.SelectedText = _replaceTextBox.Text;
        DoFind();
    }

    private void DoReplaceAll()
    {
        if (ActiveEditor == null) return;
        string searchText = _findTextBox.Text;
        string replaceText = _replaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        int count = 0, index = 0;
        while (true)
        {
            index = ActiveEditor.Find(searchText, index, FindOptions);
            if (index == -1) break;
            ActiveEditor.Select(index, searchText.Length);
            ActiveEditor.SelectedText = replaceText;
            index += Math.Max(replaceText.Length, searchText.Length);
            count++;
        }
        _statusLabel.Text = $"{count}개 항목을 바꿨습니다.";
    }

    #endregion

    #region File Operations

    private void OnNew(object? sender, EventArgs e) => NewDocument();

    private void OnOpen(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "모든 지원 파일|*.txt;*.rtf;*.html;*.htm|텍스트 파일 (*.txt)|*.txt|서식 있는 텍스트 (*.rtf)|*.rtf|HTML (*.html)|*.html;*.htm|모든 파일 (*.*)|*.*",
            FilterIndex = 1
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            OpenFileInNewTab(dlg.FileName);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (ActiveDoc == null) return;
        if (ActiveDoc.FilePath == null)
            OnSaveAs(sender, e);
        else
            SaveDocument(_activeDocIndex, ActiveDoc.FilePath);
    }

    private void OnSaveAs(object? sender, EventArgs e)
    {
        if (ActiveDoc == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|서식 있는 텍스트 (*.rtf)|*.rtf|HTML (*.html)|*.html",
            FilterIndex = 1
        };

        if (ActiveDoc.FilePath != null)
        {
            dlg.FileName = Path.GetFileName(ActiveDoc.FilePath);
            string ext = Path.GetExtension(ActiveDoc.FilePath).ToLowerInvariant();
            dlg.FilterIndex = ext switch
            {
                ".rtf" => 2,
                ".html" or ".htm" => 3,
                _ => 1
            };
        }

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveDocument(_activeDocIndex, dlg.FileName);
        }
    }

    private void SaveDocument(int index, string path)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];

        if (doc.IsLargeFile || doc.Editor == null)
        {
            MessageBox.Show(
                "대용량 모드로 연 파일은 읽기 전용입니다.\n편집 후 저장하려면 일반 크기의 파일을 사용하세요.",
                _appTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();

        // Check if saving to text-only format while content has images
        if ((ext == ".txt" || ext == ".html" || ext == ".htm") && DocumentHasImages(doc.Editor))
        {
            var result = MessageBox.Show(
                "이 파일에는 이미지가 포함되어 있습니다.\n" +
                $"{ext} 형식으로 저장하면 이미지와 서식이 모두 제거되고 텍스트만 저장됩니다.\n\n" +
                "계속하시겠습니까?",
                "경고 - 데이터 손실",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;
        }

        try
        {
            switch (ext)
            {
                case ".rtf":
                    doc.Editor.SaveFile(path, RichTextBoxStreamType.RichText);
                    break;
                case ".ymemo":
                    SaveYmemoFrom(doc.Editor, path);
                    break;
                case ".html":
                case ".htm":
                    string html = RtfHelper.ConvertToHtml(doc.Editor);
                    File.WriteAllText(path, html, Encoding.UTF8);
                    break;
                default: // .txt and others
                    File.WriteAllText(path, doc.Editor.Text, Encoding.UTF8);
                    break;
            }

            doc.FilePath = path;
            doc.Title = Path.GetFileName(path);
            doc.IsModified = false;
            UpdateTabTitle(index);
            UpdateTitle();
            _statusLabel.Text = $"저장됨: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 저장할 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool DocumentHasImages(RichTextBox? editor)
    {
        if (editor == null) return false;
        string rtf = editor.Rtf ?? "";
        return rtf.Contains("\\pict") || rtf.Contains("\\object");
    }

    private void OnCloseTab(object? sender, EventArgs e)
    {
        CloseDocument(_activeDocIndex);
    }

    private void OnPrint(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var preview = new PrintPreviewForm(ActiveEditor);
        preview.ShowDialog(this);
    }

    private void OnExit(object? sender, EventArgs e) => Close();

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        for (int i = _documents.Count - 1; i >= 0; i--)
        {
            if (_documents[i].IsModified)
            {
                SwitchToDocument(i);
                var result = MessageBox.Show(
                    $"'{_documents[i].Title}'의 변경 내용을 저장하시겠습니까?",
                    _appTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Cancel) { e.Cancel = true; return; }
                if (result == DialogResult.Yes)
                {
                    OnSave(null, EventArgs.Empty);
                    if (_documents[i].IsModified) { e.Cancel = true; return; }
                }
            }
        }

        // Save window settings before closing
        SaveSettings();
        base.OnFormClosing(e);
    }

    #endregion

    #region Edit Operations

    private void OnUndo(object? sender, EventArgs e) => ActiveEditor?.Undo();
    private void OnRedo(object? sender, EventArgs e) => ActiveEditor?.Redo();
    private void OnCut(object? sender, EventArgs e) => ActiveEditor?.Cut();
    private void OnCopy(object? sender, EventArgs e) => ActiveEditor?.Copy();

    private void OnPaste(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;

        // RTF나 HTML 형식이 있으면 서식+이미지 포함 붙여넣기 (혼합 콘텐츠 지원)
        if (Clipboard.ContainsData(DataFormats.Rtf))
        {
            ActiveEditor.Paste(DataFormats.GetFormat(DataFormats.Rtf));
        }
        else if (Clipboard.ContainsData(DataFormats.Html))
        {
            // HTML 형식 → RichTextBox가 변환 처리
            ActiveEditor.Paste();
        }
        else if (Clipboard.ContainsImage())
        {
            // 순수 이미지만 있는 경우 (스크린샷 등)
            PasteClipboardImage();
        }
        else
        {
            ActiveEditor.Paste();
        }
    }

    private void OnSelectAll(object? sender, EventArgs e) => ActiveEditor?.SelectAll();

    private void OnToggleFind(object? sender, EventArgs e)
    {
        _findPanel.Visible = !_findPanel.Visible;
        if (_findPanel.Visible)
        {
            _findTextBox.Focus();
            if (ActiveEditor != null && ActiveEditor.SelectedText.Length > 0)
                _findTextBox.Text = ActiveEditor.SelectedText;
        }
    }

    #endregion

    #region Format Operations

    private void OnBold(object? sender, EventArgs e) => ToggleFontStyle(FontStyle.Bold);
    private void OnItalic(object? sender, EventArgs e) => ToggleFontStyle(FontStyle.Italic);
    private void OnUnderline(object? sender, EventArgs e) => ToggleFontStyle(FontStyle.Underline);
    private void OnStrikethrough(object? sender, EventArgs e) => ToggleFontStyle(FontStyle.Strikeout);

    private void ToggleFontStyle(FontStyle style)
    {
        if (ActiveEditor == null) return;
        var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
        FontStyle newStyle = currentFont.Style.HasFlag(style)
            ? currentFont.Style & ~style
            : currentFont.Style | style;
        ApplySelectionFont(new Font(currentFont.FontFamily, currentFont.Size, newStyle));
    }

    private void OnTextColor(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new ColorDialog { Color = _currentTextColor };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _currentTextColor = dlg.Color;
            ActiveEditor.SelectionColor = _currentTextColor;
            _textColorBtn.ForeColor = _currentTextColor;
        }
    }

    private void OnBackColor(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new ColorDialog { Color = _currentBgColor };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _currentBgColor = dlg.Color;
            ActiveEditor.SelectionBackColor = _currentBgColor;
            _bgColorBtn.ForeColor = _currentBgColor;
        }
    }

    private void OnAlignLeft(object? sender, EventArgs e) { if (ActiveEditor != null) ActiveEditor.SelectionAlignment = HorizontalAlignment.Left; }
    private void OnAlignCenter(object? sender, EventArgs e) { if (ActiveEditor != null) ActiveEditor.SelectionAlignment = HorizontalAlignment.Center; }
    private void OnAlignRight(object? sender, EventArgs e) { if (ActiveEditor != null) ActiveEditor.SelectionAlignment = HorizontalAlignment.Right; }

    private void OnBulletList(object? sender, EventArgs e)
    {
        if (ActiveEditor != null) ActiveEditor.SelectionBullet = !ActiveEditor.SelectionBullet;
    }

    private void OnIndent(object? sender, EventArgs e)
    {
        if (ActiveEditor != null) ActiveEditor.SelectionIndent += 20;
    }

    private void OnOutdent(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        ActiveEditor.SelectionIndent = Math.Max(0, ActiveEditor.SelectionIndent - 20);
    }

    private void OnFont(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new FontDialog
        {
            Font = ActiveEditor.SelectionFont ?? ActiveEditor.Font,
            ShowColor = true,
            Color = ActiveEditor.SelectionColor
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            ActiveEditor.SelectionFont = dlg.Font;
            ActiveEditor.SelectionColor = dlg.Color;
        }
    }

    private void OnToggleWordWrap(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        ActiveEditor.WordWrap = !ActiveEditor.WordWrap;
        _statusLabel.Text = ActiveEditor.WordWrap ? "자동 줄 바꿈: 켜짐" : "자동 줄 바꿈: 꺼짐";
    }

    private void FontFamily_Changed(object? sender, EventArgs e)
    {
        if (ActiveEditor == null || _fontFamilyCombo.SelectedItem == null) return;
        string fontName = _fontFamilyCombo.SelectedItem.ToString()!;
        var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
        try { ApplySelectionFont(new Font(fontName, currentFont.Size, currentFont.Style)); } catch { }
    }

    private void FontSize_Changed(object? sender, EventArgs e) => ApplyFontSize();
    private void FontSize_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { ApplyFontSize(); e.SuppressKeyPress = true; }
    }

    private void ApplyFontSize()
    {
        if (ActiveEditor == null) return;
        if (float.TryParse(_fontSizeCombo.Text, out float size) && size >= 1 && size <= 200)
        {
            var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
            ApplySelectionFont(new Font(currentFont.FontFamily, size, currentFont.Style));
        }
    }

    private void Paragraph_Changed(object? sender, EventArgs e)
    {
        if (ActiveEditor == null || _paragraphCombo.SelectedIndex < 0) return;
        var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
        var (size, style) = _paragraphCombo.SelectedIndex switch
        {
            1 => (24f, FontStyle.Bold),
            2 => (18f, FontStyle.Bold),
            3 => (14f, FontStyle.Bold),
            _ => (12f, FontStyle.Regular)
        };
        ApplySelectionFont(new Font(currentFont.FontFamily, size, style));
    }

    #endregion

    #region Image Operations

    private void OnInsertImageFile(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new OpenFileDialog
        {
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일|*.*",
            Title = "삽입할 이미지 선택"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using var img = Image.FromFile(dlg.FileName);
                InsertImageAtOriginalSize(img);
                _statusLabel.Text = $"이미지 삽입됨: {Path.GetFileName(dlg.FileName)} ({img.Width}×{img.Height})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지를 삽입할 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnPasteClipboardImage(object? sender, EventArgs e) => PasteClipboardImage();

    private void PasteClipboardImage()
    {
        if (ActiveEditor == null) return;
        if (Clipboard.ContainsImage())
        {
            using var img = Clipboard.GetImage();
            if (img != null)
            {
                InsertImageAtOriginalSize(img);
                _statusLabel.Text = $"클립보드 이미지 삽입됨 ({img.Width}×{img.Height})";
            }
        }
        else
        {
            MessageBox.Show("클립보드에 이미지가 없습니다.", _appTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// 이미지를 원본 크기 그대로 RTF에 삽입합니다.
    /// RichTextBox의 기본 Paste()는 이미지를 축소할 수 있으므로,
    /// RTF를 직접 생성하여 원본 크기를 보장합니다.
    /// </summary>
    private void InsertImageAtOriginalSize(Image img)
    {
        if (ActiveEditor == null) return;

        // 이미지를 PNG 바이트로 변환
        using var ms = new MemoryStream();
        img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        byte[] imgBytes = ms.ToArray();

        // 원본 크기를 twips로 변환 (1 inch = 1440 twips, 96 DPI 기준)
        int picwgoal = (int)(img.Width * 1440f / img.HorizontalResolution);
        int pichgoal = (int)(img.Height * 1440f / img.VerticalResolution);

        // RTF 이미지 문자열 생성
        string hexData = BitConverter.ToString(imgBytes).Replace("-", "");
        string rtfImage = @"{\rtf1 {\pict\pngblip\picwgoal" + picwgoal + @"\pichgoal" + pichgoal + " " + hexData + "}}";

        IDataObject? savedClipboard = null;
        try { savedClipboard = Clipboard.GetDataObject(); } catch { }

        try
        {
            Clipboard.SetData(DataFormats.Rtf, rtfImage);
            ActiveEditor.Paste(DataFormats.GetFormat(DataFormats.Rtf));
        }
        finally
        {
            try
            {
                if (savedClipboard != null)
                    Clipboard.SetDataObject(savedClipboard, true);
            }
            catch { }
        }
    }

    #endregion

    #region Insert Operations

    private void OnInsertLink(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new LinkDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            string linkText = string.IsNullOrEmpty(dlg.LinkText) ? dlg.LinkUrl : dlg.LinkText;
            RtfHelper.InsertHyperlink(ActiveEditor, linkText, dlg.LinkUrl);
            _statusLabel.Text = $"링크 삽입됨: {dlg.LinkUrl} (Ctrl+클릭으로 열기)";
        }
    }

    private void OnInsertHorizontalRule(object? sender, EventArgs e)
    {
        if (ActiveEditor != null)
            ActiveEditor.SelectedText = "\n────────────────────────────────────────\n";
    }

    private void OnInsertTable(object? sender, EventArgs e)
    {
        if (ActiveEditor == null) return;
        using var dlg = new TableDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            ActiveEditor.SelectedText = GenerateTextTable(dlg.Rows, dlg.Cols);
            _statusLabel.Text = $"표 삽입됨: {dlg.Rows}행 × {dlg.Cols}열";
        }
    }

    private string GenerateTextTable(int rows, int cols)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        string sep = "+"; string hdr = "|";
        for (int c = 0; c < cols; c++) { sep += "----------+"; hdr += $" 열{c + 1,-6}  |"; }
        sb.AppendLine(sep); sb.AppendLine(hdr); sb.AppendLine(sep);
        for (int r = 1; r < rows; r++) { string row = "|"; for (int c = 0; c < cols; c++) row += "          |"; sb.AppendLine(row); }
        sb.AppendLine(sep); sb.AppendLine();
        return sb.ToString();
    }

    #endregion

    #region Ymemo Format

    private void SaveYmemoFrom(RichTextBox editor, string path)
    {
        using var ms = new MemoryStream();
        editor.SaveFile(ms, RichTextBoxStreamType.RichText);
        byte[] rtfData = ms.ToArray();
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);
        writer.Write(Encoding.ASCII.GetBytes("YMEMO1\n"));
        writer.Write(rtfData.Length);
        writer.Write(rtfData);
    }

    private void LoadYmemoInto(RichTextBox editor, string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        byte[] headerBytes = reader.ReadBytes(7);
        string header = Encoding.ASCII.GetString(headerBytes);
        if (!header.StartsWith("YMEMO1"))
            throw new InvalidDataException("유효하지 않은 .ymemo 파일입니다.");
        int rtfLength = reader.ReadInt32();
        byte[] rtfData = reader.ReadBytes(rtfLength);
        using var ms = new MemoryStream(rtfData);
        editor.LoadFile(ms, RichTextBoxStreamType.RichText);
    }

    #endregion

    #region Help

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "윤슬 에디터 v1.1\n\n" +
            "위지위그(WYSIWYG) 멀티탭 텍스트 편집기\n\n" +
            "주요 기능:\n" +
            "• 좌측 파일 브라우저\n" +
            "• 멀티 문서 탭 (하단 탭 바)\n" +
            "• 굵게/기울임/밑줄/취소선\n" +
            "• 글꼴, 크기, 색상 변경\n" +
            "• 문단 서식, 정렬, 글머리 기호\n" +
            "• 이미지 삽입 (파일/클립보드) - 원본 크기\n" +
            "• 하이퍼링크 삽입 (Ctrl+클릭으로 열기)\n" +
            "• 수평선, 표 삽입\n" +
            "• 찾기/바꾸기\n" +
            "• 인쇄 미리보기 (여백 조정)\n" +
            "• A4 용지 윤곽선 가이드\n\n" +
            "지원 형식: .txt, .rtf, .html",
            "윤슬 에디터 정보",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion

    #region Editor Event Handlers

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (ActiveEditor == null || sender != ActiveEditor) return;
        if (!ActiveDoc.IsModified)
        {
            ActiveDoc.IsModified = true;
            UpdateTabTitle(_activeDocIndex);
            UpdateTitle();
        }
        UpdateWordCount();
    }

    private void Editor_SelectionChanged(object? sender, EventArgs e)
    {
        if (ActiveEditor == null || sender != ActiveEditor) return;
        UpdateCursorPosition();
        UpdateFormatToolbarState();
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.V)
        {
            PasteClipboardImage();
            e.Handled = true;
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _findPanel.Visible)
        {
            _findPanel.Visible = false;
            e.Handled = true;
        }
    }

    private void SplitContainer_Paint(object? sender, PaintEventArgs e)
    {
        // Draw a subtle vertical line in the center of the splitter
        var rect = _splitContainer.SplitterRectangle;
        int cx = rect.Left + rect.Width / 2;
        using var pen = new Pen(Color.FromArgb(180, 180, 175), 1f);
        e.Graphics.DrawLine(pen, cx, rect.Top + 4, cx, rect.Bottom - 4);

        // Draw small grip dots in the center
        int cy = rect.Top + rect.Height / 2;
        using var dotBrush = new SolidBrush(Color.FromArgb(160, 160, 155));
        for (int i = -2; i <= 2; i++)
        {
            e.Graphics.FillEllipse(dotBrush, cx - 1, cy + i * 6 - 1, 3, 3);
        }
    }

    /// <summary>
    /// A4 용지 윤곽선을 부드럽게 그립니다.
    /// 편집 영역 주변에 용지 테두리를 표시합니다.
    /// </summary>
    private void PageOutlinePanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var pad = _pageOutlinePanel.Padding;
        // 에디터 영역 (패딩 안쪽)의 테두리를 그림
        var editorRect = new Rectangle(
            pad.Left - 1, pad.Top - 1,
            _pageOutlinePanel.ClientSize.Width - pad.Left - pad.Right + 2,
            _pageOutlinePanel.ClientSize.Height - pad.Top - pad.Bottom + 2);

        // 부드러운 그림자 효과
        using var shadowPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
        var shadowRect = new Rectangle(editorRect.X + 2, editorRect.Y + 2, editorRect.Width, editorRect.Height);
        g.DrawRectangle(shadowPen, shadowRect);

        // 용지 테두리 (부드러운 라운드 효과)
        using var borderPen = new Pen(Color.FromArgb(180, 190, 190, 185), 1.2f);
        g.DrawRectangle(borderPen, editorRect);
    }

    private void UpdateFormatToolbarState()
    {
        if (ActiveEditor == null) return;
        var font = ActiveEditor.SelectionFont;
        if (font != null)
        {
            _boldBtn.Checked = font.Bold;
            _italicBtn.Checked = font.Italic;
            _underlineBtn.Checked = font.Underline;
            _strikeBtn.Checked = font.Strikeout;
            _fontSizeCombo.Text = font.Size.ToString("0");

            for (int i = 0; i < _fontFamilyCombo.Items.Count; i++)
            {
                if (_fontFamilyCombo.Items[i]?.ToString() == font.FontFamily.Name)
                { _fontFamilyCombo.SelectedIndex = i; break; }
            }
        }
        _alignLeftBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Left;
        _alignCenterBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Center;
        _alignRightBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Right;
        _bulletListBtn.Checked = ActiveEditor.SelectionBullet;
    }

    private void UpdateCursorPosition()
    {
        if (ActiveDoc != null && ActiveDoc.IsLargeFile)
        {
            var viewer = ActiveDoc.LargeViewer!;
            _lineColLabel.Text = $"줄: {viewer.CurrentLine + 1} / {viewer.TotalLines:N0}";
            return;
        }
        if (ActiveEditor == null) return;
        int charIndex = ActiveEditor.SelectionStart;
        int line = ActiveEditor.GetLineFromCharIndex(charIndex);
        int col = charIndex - ActiveEditor.GetFirstCharIndexFromLine(line);
        _lineColLabel.Text = $"줄: {line + 1}, 열: {col + 1}";
    }

    #endregion

    #region Helpers

    private static void ApplySelectionFont(RichTextBox editor, Font font)
    {
        editor.SelectionFont = font;
        font.Dispose();
    }

    private void ApplySelectionFont(Font font)
    {
        if (ActiveEditor != null)
            ApplySelectionFont(ActiveEditor, font);
        else
            font.Dispose();
    }

    private ToolStripMenuItem CreateMenuItem(string text, Keys shortcut, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
        item.Click += handler;
        return item;
    }

    private ToolStripButton CreateToolButton(string tooltip, string text, EventHandler handler)
    {
        var btn = new ToolStripButton(text)
        {
            ToolTipText = tooltip,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2)
        };
        btn.Click += handler;
        return btn;
    }

    private void UpdateTitle()
    {
        if (ActiveDoc == null) { Text = _appTitle; return; }
        string fileName = ActiveDoc.FilePath != null ? Path.GetFileName(ActiveDoc.FilePath) : ActiveDoc.Title;
        string modified = ActiveDoc.IsModified ? "*" : "";
        Text = $"{modified}{fileName} - {_appTitle}";
    }

    private void UpdateWordCount()
    {
        if (ActiveDoc != null && ActiveDoc.IsLargeFile)
        {
            _wordCountLabel.Text = $"{ActiveDoc.LargeViewer!.TotalLines:N0} 줄 (대용량 모드)";
            return;
        }
        if (ActiveEditor == null) { _wordCountLabel.Text = ""; return; }
        string text = ActiveEditor.Text;
        int charCount = text.Length;
        int wordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        _wordCountLabel.Text = $"{wordCount} 단어 | {charCount} 자";
    }

    #endregion

    #region Settings Persistence

    private void SaveSettings()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var lines = new List<string>
            {
                $"SplitterDistance={_splitContainer.SplitterDistance}",
                $"WindowWidth={Width}",
                $"WindowHeight={Height}",
                $"WindowLeft={Left}",
                $"WindowTop={Top}",
                $"WindowState={WindowState}"
            };
            File.WriteAllLines(_settingsPath, lines, Encoding.UTF8);
        }
        catch { }
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var settings = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(_settingsPath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                    settings[line[..eq]] = line[(eq + 1)..];
            }

            if (settings.TryGetValue("WindowState", out var stateStr) &&
                Enum.TryParse<FormWindowState>(stateStr, out var state))
            {
                if (state == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Maximized;
                }
                else
                {
                    if (settings.TryGetValue("WindowWidth", out var w) && int.TryParse(w, out int width))
                        Width = Math.Max(400, width);
                    if (settings.TryGetValue("WindowHeight", out var h) && int.TryParse(h, out int height))
                        Height = Math.Max(300, height);
                    if (settings.TryGetValue("WindowLeft", out var l) && int.TryParse(l, out int left))
                        Left = Math.Max(0, left);
                    if (settings.TryGetValue("WindowTop", out var t) && int.TryParse(t, out int top))
                        Top = Math.Max(0, top);
                    StartPosition = FormStartPosition.Manual;
                }
            }

            if (settings.TryGetValue("SplitterDistance", out var sd) && int.TryParse(sd, out int splitter))
            {
                _splitContainer.SplitterDistance = Math.Clamp(splitter, 50, Width - 200);
            }
        }
        catch { }
    }

    #endregion
}

/// <summary>
/// 열린 문서 탭 하나를 나타냅니다.
/// </summary>
public class DocumentTab
{
    public string Title { get; set; } = "";
    public string? FilePath { get; set; }
    public bool IsModified { get; set; }
    public RichTextBox? Editor { get; set; }

    /// <summary>대용량 파일 모드일 때 사용하는 뷰어 (null이면 일반 모드)</summary>
    public LargeFileViewer? LargeViewer { get; set; }

    /// <summary>대용량 모드 여부</summary>
    public bool IsLargeFile => LargeViewer != null;
}
