using System.Diagnostics;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace YunseulMemo;

public partial class MainForm : Form
{
    private const string _appName = "윤슬 에디터";
    private readonly string _appTitle = "윤슬 에디터 v1.2";

    // 멀티 문서
    private readonly List<DocumentTab> _documents = new();
    private int _activeDocIndex = -1;
    private int _untitledCounter = 0;

    private string _currentBrowsePath = "";
    private readonly string _settingsPath;

    // 설정
    private Font _editorFont = null!;
    private bool _wordWrap = true;
    private bool _showRuler = true;
    private bool _showLineNumbers = true;
    private bool _showDirPane = true;
    private bool _showStatusBar = true;
    private bool _showMainTool = true;
    private bool _showFormatTool = true;
    private readonly List<string> _recentFiles = new();
    private readonly List<string> _clipItems = new();
    private bool _clipLoaded;
    private Rectangle? _pendingBounds;
    private FormWindowState? _pendingState;
    private int _pendingSplitterDistance = -1;
    private int _lastSplitter = -1;

    // 상태 플래그
    private bool _updatingToolbar;
    private bool _suppressModified;
    private readonly System.Windows.Forms.Timer _countTimer = new() { Interval = 250 };

    private FindReplaceForm? _findForm;

    // 컨텍스트 메뉴
    private readonly ContextMenuStrip _tabContextMenu = new();
    private readonly ContextMenuStrip _editorContextMenu = new();
    private ToolStripMenuItem _miEditCut = null!;
    private ToolStripMenuItem _miEditCopy = null!;
    private ToolStripMenuItem _miEditPaste = null!;
    private ToolStripMenuItem _miEditTableProps = null!;

    private static readonly string[] DefaultClips =
    {
        "$(DATE)", "$(TIME)", "$(DATETIME)",
        "────────────────────────────────────────",
        "========================================",
        "TODO: ", "※ "
    };

    public MainForm(string[]? startupFiles = null)
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YunseulMemo", "settings.ini");

        _editorFont = PickEditorFont();
        LoadSettings();
        InitializeComponent();
        ApplyWindowSettings();
        ApplyViewSettings();
        InitContextMenus();

        _splitContainer.SplitterMoved += (_, _) =>
        {
            if (!_splitContainer.Panel1Collapsed) _lastSplitter = _splitContainer.SplitterDistance;
        };
        _countTimer.Tick += (_, _) => { _countTimer.Stop(); UpdateWordCount(); UpdateCursorPosition(); };

        NewDocument();

        if (startupFiles != null)
        {
            foreach (var f in startupFiles)
            {
                try { if (File.Exists(f)) OpenFileInNewTab(Path.GetFullPath(f)); } catch { }
            }
        }
    }

    /// <summary>EditPlus처럼 고정폭 글꼴을 기본으로 사용합니다 (D2Coding → 굴림체 → Consolas → Courier New).</summary>
    private static Font PickEditorFont()
    {
        string[] candidates = { "D2Coding", "굴림체", "GulimChe", "Consolas", "Courier New" };
        using var ifc = new InstalledFontCollection();
        var names = new HashSet<string>(ifc.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
            if (names.Contains(c)) return new Font(c, 10f);
        return new Font(FontFamily.GenericMonospace, 10f);
    }

    // ───────────────────────── 보기 설정 ─────────────────────────

    private void ApplyViewSettings()
    {
        if (_menuStrip == null) return;
        _mainToolStrip.Visible = _showMainTool;
        _formatToolStrip.Visible = _showFormatTool;
        _statusStrip.Visible = _showStatusBar;
        _splitContainer.Panel1Collapsed = !_showDirPane;

        bool textMode = _activeDocIndex < 0 || _activeDocIndex >= _documents.Count || !_documents[_activeDocIndex].IsLargeFile;
        _ruler.Visible = _showRuler && textMode;
        _lineNumberPanel.Visible = _showLineNumbers && textMode;
        SyncRuler();
        UpdateViewToggles();
    }

    private void UpdateViewToggles()
    {
        _miMainTool.Checked = _showMainTool;
        _miFormatTool.Checked = _showFormatTool;
        _miStatusBar.Checked = _showStatusBar;
        _miDirPane.Checked = _showDirPane;
        _miRuler.Checked = _showRuler;
        _miLineNum.Checked = _showLineNumbers;
        _miWrap.Checked = _wordWrap;
        _rulerBtn.Checked = _showRuler;
        _lineNumBtn.Checked = _showLineNumbers;
        _wrapBtn.Checked = _wordWrap;
    }

    private void OnToggleWordWrap(object? sender, EventArgs e)
    {
        _wordWrap = !_wordWrap;
        _suppressModified = true;
        try
        {
            foreach (var d in _documents)
                if (d.Editor != null) d.Editor.WordWrap = _wordWrap;
        }
        finally { _suppressModified = false; }
        UpdateViewToggles();
        _ruler.Invalidate();
        _statusLabel.Text = _wordWrap ? "자동 줄 바꿈: 켜짐" : "자동 줄 바꿈: 꺼짐";
    }

    private void OnEditorFontSetting(object? sender, EventArgs e)
    {
        using var dlg = new FontDialog { Font = _editorFont, FontMustExist = true, ShowEffects = false };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _editorFont = (Font)dlg.Font.Clone();
        _lineNumberPanel.Font = _editorFont;
        _suppressModified = true;
        try
        {
            // 서식 있는 문서(RTF/이미지 포함)는 건드리지 않고, 일반 텍스트 문서에만 적용
            foreach (var d in _documents)
                if (d.Editor != null && !d.IsRich && !DocumentHasImages(d.Editor))
                    d.Editor.Font = _editorFont;
        }
        finally { _suppressModified = false; }
        _ruler.Invalidate();
        _lineNumberPanel.Invalidate();
        UpdateFormatToolbarState();
        _statusLabel.Text = $"편집창 글꼴: {_editorFont.Name}, {_editorFont.Size:0.#}pt (새 문서와 일반 텍스트 문서에 적용)";
    }

    // ───────────────────────── 디렉토리 창 ─────────────────────────

    private void LoadDriveTree()
    {
        _folderTree.Nodes.Clear();
        if (_driveCombo.SelectedItem == null) return;

        string drivePath = _driveCombo.SelectedItem.ToString()!;
        var rootNode = new TreeNode(drivePath) { Tag = drivePath, ImageIndex = 0, SelectedImageIndex = 1 };
        rootNode.Nodes.Add("");
        _folderTree.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private void DriveCombo_Changed(object? sender, EventArgs e) => LoadDriveTree();

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
                var dirs = Directory.GetDirectories(path);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                foreach (var dir in dirs)
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.StartsWith("$") || folderName.Contains('%')) continue;

                    try
                    {
                        var attrs = new DirectoryInfo(dir).Attributes;
                        if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System)) continue;
                    }
                    catch { continue; }

                    var child = new TreeNode(folderName) { Tag = dir, ImageIndex = 0, SelectedImageIndex = 1 };
                    child.Nodes.Add("");
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
        _fileList.BeginUpdate();
        try
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
                var files = Directory.GetFiles(_currentBrowsePath, pattern);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    int img = ext switch { ".rtf" => 1, ".html" or ".htm" => 2, _ => 0 };
                    _fileList.Items.Add(new ListViewItem(Path.GetFileName(file), img));
                }
            }
            catch { }
        }
        finally { _fileList.EndUpdate(); }
    }

    private void FileList_DoubleClick(object? sender, EventArgs e)
    {
        if (_fileList.SelectedItems.Count == 0) return;
        string fullPath = Path.Combine(_currentBrowsePath, _fileList.SelectedItems[0].Text);
        OpenFileInNewTab(fullPath);
    }

    // ───────────────────────── 클립텍스트 ─────────────────────────

    private static string ClipDisplay(string clip) => clip switch
    {
        "$(DATE)" => "[현재 날짜]",
        "$(TIME)" => "[현재 시간]",
        "$(DATETIME)" => "[현재 날짜 + 시간]",
        _ => ClipPreview(clip)
    };

    private static string ClipPreview(string clip)
    {
        string s = clip.Replace("\r", "").Replace('\n', '↵');
        return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
    }

    private static string ResolveClip(string clip)
    {
        var now = DateTime.Now;
        return clip
            .Replace("$(DATETIME)", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Replace("$(DATE)", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Replace("$(TIME)", now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private void RebuildClipList()
    {
        if (!_clipLoaded) { _clipItems.AddRange(DefaultClips); _clipLoaded = true; }
        _clipList.Items.Clear();
        foreach (var c in _clipItems) _clipList.Items.Add(ClipDisplay(c));
    }

    private void InsertSelectedClip()
    {
        int i = _clipList.SelectedIndex;
        var ed = ActiveEditor;
        if (i < 0 || i >= _clipItems.Count || ed == null) return;
        ed.SelectedText = ResolveClip(_clipItems[i]).Replace("\r\n", "\n");
        ed.Focus();
    }

    private void AddSelectionToClip()
    {
        var ed = ActiveEditor;
        if (ed == null || ed.SelectedText.Length == 0) { _statusLabel.Text = "클립텍스트로 추가할 텍스트를 먼저 선택하세요."; return; }
        _clipItems.Add(ed.SelectedText);
        RebuildClipList();
        _statusLabel.Text = "클립텍스트에 추가했습니다.";
    }

    private void RemoveSelectedClip()
    {
        int i = _clipList.SelectedIndex;
        if (i < 0 || i >= _clipItems.Count) return;
        _clipItems.RemoveAt(i);
        RebuildClipList();
    }

    // ───────────────────────── 멀티 문서 / 탭 ─────────────────────────

    private DocumentTab ActiveDoc => _activeDocIndex >= 0 && _activeDocIndex < _documents.Count
        ? _documents[_activeDocIndex] : null!;

    private RichTextBox? ActiveEditor => ActiveDoc?.Editor;

    private static Color? AccentFor(string? path)
    {
        string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext switch
        {
            ".rtf" or ".ymemo" => Color.FromArgb(60, 120, 210),
            ".html" or ".htm" => Color.FromArgb(230, 120, 40),
            _ => null
        };
    }

    private static bool IsPristineUntitled(DocumentTab d) =>
        d.FilePath == null && !d.IsModified && !d.IsLargeFile && d.Editor != null && d.Editor.TextLength == 0;

    private void AddDocument(DocumentTab doc)
    {
        // EditPlus처럼: 아무것도 입력하지 않은 빈 새 문서 하나뿐일 때 파일을 열면 그 빈 문서를 대체합니다.
        DocumentTab? replace = null;
        if (doc.FilePath != null && _documents.Count == 1 && IsPristineUntitled(_documents[0]))
            replace = _documents[0];

        _documents.Add(doc);
        _docTabs.AddTab(doc.Title, AccentFor(doc.FilePath), doc.FilePath ?? doc.Title);
        SwitchToDocument(_documents.Count - 1);

        if (replace != null)
        {
            int i = _documents.IndexOf(replace);
            if (i >= 0) CloseDocument(i);
        }
    }

    private void NewDocument()
    {
        _untitledCounter++;
        var doc = new DocumentTab
        {
            Title = $"제목 없음{_untitledCounter}",
            FilePath = null,
            IsModified = false,
            Editor = CreateEditor(),
            Encoding = TextEnc.Utf8Bom,
            Eol = EolKind.Pc
        };
        AddDocument(doc);
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

        const long LargeFileThreshold = 5 * 1024 * 1024;
        FileInfo fileInfo;
        try { fileInfo = new FileInfo(path); if (!fileInfo.Exists) throw new FileNotFoundException(path); }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 열 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();
        bool isTextFile = ext is ".txt" or ".log" or ".csv" or ".tsv" or ".sql" or ".json" or ".xml" or ".md" or "";
        if (fileInfo.Length > LargeFileThreshold && isTextFile)
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
                    doc.IsRich = true;
                    break;
                case ".ymemo":
                    LoadYmemoInto(doc.Editor, path);
                    doc.IsRich = true;
                    break;
                default:
                {
                    var (text, enc, eol) = TextFileHelper.Read(path);
                    doc.Encoding = enc;
                    doc.Eol = eol;
                    doc.HtmlSource = ext is ".html" or ".htm";
                    doc.Editor.Text = text;
                    break;
                }
            }
            doc.IsModified = false;
        }
        catch (Exception ex)
        {
            doc.Editor.Dispose();
            MessageBox.Show($"파일을 열 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        AddRecent(path);
        AddDocument(doc);
        _statusLabel.Text = $"열림: {path}";
    }

    private void OpenLargeFile(string path)
    {
        var viewer = new LargeFileViewer { Dock = DockStyle.Fill, Visible = false };
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
            LargeViewer = viewer,
            Encoding = viewer.DetectedEncoding
        };

        AddRecent(path);
        AddDocument(doc);

        long sizeMB = new FileInfo(path).Length / (1024 * 1024);
        _statusLabel.Text = $"대용량 모드로 열림: {path} ({sizeMB}MB, {viewer.TotalLines:N0}줄)";
    }

    private void SwitchToDocument(int index)
    {
        if (index < 0 || index >= _documents.Count) return;

        // 이전 문서 숨기기
        if (_activeDocIndex >= 0 && _activeDocIndex < _documents.Count && _activeDocIndex != index)
        {
            var prev = _documents[_activeDocIndex];
            Control? pc = prev.IsLargeFile ? prev.LargeViewer : prev.Editor;
            if (pc != null && pc.Parent != null) pc.Visible = false;
        }

        _activeDocIndex = index;
        var doc = _documents[index];

        if (doc.IsLargeFile)
        {
            var v = doc.LargeViewer!;
            if (v.Parent == null) { v.Dock = DockStyle.Fill; _editorHost.Controls.Add(v); }
            v.Visible = true;
            v.BringToFront();
            v.Focus();
            _lineNumberPanel.DetachEditor();
            _ruler.AttachEditor(null);
            _lineNumberPanel.Visible = false;
            _ruler.Visible = false;
        }
        else if (doc.Editor != null)
        {
            var ed = doc.Editor;
            if (ed.Parent == null) { ed.Dock = DockStyle.Fill; _editorHost.Controls.Add(ed); }
            ed.Visible = true;
            ed.BringToFront();
            ed.Focus();
            _lineNumberPanel.AttachEditor(ed, doc.Bookmarks);
            _ruler.AttachEditor(ed);
            _lineNumberPanel.Visible = _showLineNumbers;
            _ruler.Visible = _showRuler;
        }

        SyncRuler();
        _docTabs.SelectedIndex = index;

        UpdateTitle();
        UpdateWordCount();
        UpdateCursorPosition();
        UpdateDocMeta();
        UpdateFormatToolbarState();
        UpdateViewToggles();
    }

    private void DocTabs_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int i = _docTabs.SelectedIndex;
        if (i >= 0 && i != _activeDocIndex) SwitchToDocument(i);
    }

    private void DocTabs_TabMoved(object? sender, (int From, int To) a)
    {
        if (a.From < 0 || a.From >= _documents.Count || a.To < 0 || a.To >= _documents.Count) return;
        var d = _documents[a.From];
        _documents.RemoveAt(a.From);
        _documents.Insert(a.To, d);
        _activeDocIndex = _docTabs.SelectedIndex;
    }

    private void InitContextMenus()
    {
        _tabContextMenu.Renderer = new EditPlusRenderer();
        _editorContextMenu.Renderer = new EditPlusRenderer();

        _miEditCut = new ToolStripMenuItem("잘라내기(&T)", null, (_, _) => ActiveEditor?.Cut(), Keys.Control | Keys.X);
        _miEditCopy = new ToolStripMenuItem("복사(&C)", null, (_, _) => ActiveEditor?.Copy(), Keys.Control | Keys.C);
        _miEditPaste = new ToolStripMenuItem("붙여넣기(&P)", null, (_, _) => { if (ActiveEditor != null) HandleCustomPaste(ActiveEditor); }, Keys.Control | Keys.V);
        var miDelete = new ToolStripMenuItem("삭제(&D)", null, (_, _) => { if (ActiveEditor != null) ActiveEditor.SelectedText = ""; }, Keys.Delete);
        var miSelectAll = new ToolStripMenuItem("모두 선택(&A)", null, (_, _) => ActiveEditor?.SelectAll(), Keys.Control | Keys.A);

        _miEditTableProps = new ToolStripMenuItem("표 서식 및 속성 변경(&E)...", null, (_, _) => OnInsertOrEditTable(true));
        var miInsertTable = new ToolStripMenuItem("표 삽입(&I)...", null, (_, _) => OnInsertOrEditTable(false));

        _editorContextMenu.Items.AddRange(new ToolStripItem[]
        {
            _miEditCut,
            _miEditCopy,
            _miEditPaste,
            miDelete,
            new ToolStripSeparator(),
            miSelectAll,
            new ToolStripSeparator(),
            _miEditTableProps,
            miInsertTable,
            new ToolStripSeparator(),
            new ToolStripMenuItem("글꼴 서식(&F)...", null, OnEditorFontSetting)
        });

        _editorContextMenu.Opening += EditorContextMenu_Opening;
    }

    private void EditorContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ActiveEditor == null) { e.Cancel = true; return; }
        bool hasSelection = ActiveEditor.SelectionLength > 0;
        _miEditCut.Enabled = hasSelection;
        _miEditCopy.Enabled = hasSelection;
        _miEditPaste.Enabled = Clipboard.ContainsText() || Clipboard.ContainsImage();
        _miEditTableProps.Enabled = TableHelper.IsCursorInTable(ActiveEditor);
    }

    private void DocTabs_ContextRequested(object? sender, (int Index, Point Location) a)
    {
        if (a.Index < 0 || a.Index >= _documents.Count) return;
        var doc = _documents[a.Index];

        _tabContextMenu.Items.Clear();
        _tabContextMenu.Items.Add("닫기", null, (_, _) => CloseDocument(_documents.IndexOf(doc)));
        _tabContextMenu.Items.Add("다른 문서 모두 닫기", null, (_, _) => CloseOthers(doc));

        int docIdx = _documents.IndexOf(doc);
        bool hasRight = docIdx >= 0 && docIdx < _documents.Count - 1;
        bool hasLeft = docIdx > 0;

        var miRight = new ToolStripMenuItem("오른쪽 문서 모두 닫기", null, (_, _) => CloseRight(doc)) { Enabled = hasRight };
        var miLeft = new ToolStripMenuItem("왼쪽 문서 모두 닫기", null, (_, _) => CloseLeft(doc)) { Enabled = hasLeft };

        _tabContextMenu.Items.Add(miRight);
        _tabContextMenu.Items.Add(miLeft);
        _tabContextMenu.Items.Add(new ToolStripSeparator());

        var copyPath = new ToolStripMenuItem("경로 복사", null, (_, _) => { try { Clipboard.SetText(doc.FilePath!); } catch { } }) { Enabled = doc.FilePath != null };
        var openFolder = new ToolStripMenuItem("탐색기에서 폴더 열기", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{doc.FilePath}\"") { UseShellExecute = true }); } catch { }
        }) { Enabled = doc.FilePath != null };
        _tabContextMenu.Items.Add(copyPath);
        _tabContextMenu.Items.Add(openFolder);

        // 재사용 메뉴이므로 닫힐 때 Dispose 호출 안 함 (ObjectDisposedException 방지)
        _tabContextMenu.Show(_docTabs, a.Location);
    }

    private void CloseRight(DocumentTab keep)
    {
        while (true)
        {
            int keepIdx = _documents.IndexOf(keep);
            if (keepIdx < 0 || keepIdx >= _documents.Count - 1) break;
            if (!CloseDocument(keepIdx + 1)) break;
        }
    }

    private void CloseLeft(DocumentTab keep)
    {
        while (true)
        {
            int keepIdx = _documents.IndexOf(keep);
            if (keepIdx <= 0) break;
            if (!CloseDocument(keepIdx - 1)) break;
        }
    }

    private void CycleDocument(int step)
    {
        if (_documents.Count < 2) return;
        int n = (_activeDocIndex + step + _documents.Count) % _documents.Count;
        SwitchToDocument(n);
    }

    private void CloseOthers(DocumentTab keep)
    {
        foreach (var d in _documents.ToList())
        {
            if (d == keep) continue;
            int i = _documents.IndexOf(d);
            if (i < 0) continue;
            if (!CloseDocument(i)) break;
        }
    }

    /// <summary>문서에 저장되지 않은 신규 입력 또는 수정된 내용이 있는지 확인합니다.</summary>
    private static bool HasUnsavedChanges(DocumentTab doc)
    {
        if (doc.IsLargeFile) return false;

        // 신규 문서(저장된 파일 경로가 없는 문서)
        if (string.IsNullOrEmpty(doc.FilePath))
        {
            // 아무 내용도 입력되지 않았거나 공백만 있는 경우 그냥 닫기 대상
            if (doc.Editor == null || doc.Editor.TextLength == 0 || string.IsNullOrWhiteSpace(doc.Editor.Text))
                return false;

            return true;
        }

        // 기존 파일인 경우 수정 플래그 확인
        return doc.IsModified;
    }

    /// <summary>문서를 닫습니다. 저장 확인에서 닫지 않기(취소)하면 false를 반환합니다.</summary>
    private bool CloseDocument(int index)
    {
        if (index < 0 || index >= _documents.Count) return false;

        var doc = _documents[index];
        if (HasUnsavedChanges(doc))
        {
            // 사용자가 내용을 직접 보고 판단할 수 있도록 해당 탭으로 전환
            SwitchToDocument(index);

            bool isNewDoc = string.IsNullOrEmpty(doc.FilePath);
            using var dlg = new CloseConfirmDialog(doc.Title, isNewDoc);
            dlg.ShowDialog(this);

            switch (dlg.SelectedAction)
            {
                case CloseConfirmDialog.ConfirmAction.SaveAndClose:
                    SaveByIndex(index);
                    // 파일 저장이 취소되었거나 여전히 미저장 상태이면 닫기 중단
                    if (HasUnsavedChanges(doc)) return false;
                    break;

                case CloseConfirmDialog.ConfirmAction.DiscardAndClose:
                    // 무시하고 닫기: 저장하지 않고 즉시 닫기 진행
                    break;

                case CloseConfirmDialog.ConfirmAction.Cancel:
                default:
                    // 닫지 않기: 전체 닫기 중단
                    return false;
            }
        }

        bool wasActive = index == _activeDocIndex;
        if (wasActive)
        {
            _lineNumberPanel.DetachEditor();
            _ruler.AttachEditor(null);
        }

        if (doc.IsLargeFile)
        {
            if (doc.LargeViewer?.Parent != null) _editorHost.Controls.Remove(doc.LargeViewer);
            doc.LargeViewer?.Dispose();
        }
        else if (doc.Editor != null)
        {
            if (doc.Editor.Parent != null) _editorHost.Controls.Remove(doc.Editor);
            doc.Editor.Dispose();
        }

        _documents.RemoveAt(index);
        _docTabs.RemoveTabAt(index);

        if (_documents.Count == 0)
        {
            _activeDocIndex = -1;
            NewDocument();
        }
        else if (wasActive)
        {
            _activeDocIndex = -1;
            SwitchToDocument(Math.Min(index, _documents.Count - 1));
        }
        else if (index < _activeDocIndex)
        {
            _activeDocIndex--;
        }
        return true;
    }

    private RichTextBox CreateEditor()
    {
        var rtb = new ResizableRichTextBox
        {
            Dock = DockStyle.Fill,
            AcceptsTab = true,
            Font = _editorFont,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = _wordWrap,
            EnableAutoDragDrop = true,
            Visible = false,
            AllowDrop = true,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        rtb.TextChanged += Editor_TextChanged;
        rtb.SelectionChanged += Editor_SelectionChanged;
        rtb.KeyDown += Editor_KeyDown;
        rtb.MouseDown += Editor_MouseDown;
        rtb.DragEnter += Files_DragEnter;
        rtb.DragDrop += Files_DragDrop;

        // Ctrl+V (WM_PASTE)를 가로채서 이미지+텍스트 혼합 붙여넣기 지원
        rtb.CustomPasteHandler = () => HandleCustomPaste(rtb);
        rtb.ContextMenuStrip = _editorContextMenu;
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
        return false;
    }

    private void UpdateTabTitle(int index)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];
        string title = doc.FilePath != null ? Path.GetFileName(doc.FilePath) : doc.Title;
        _docTabs.SetTab(index, title, doc.IsModified, AccentFor(doc.FilePath), doc.FilePath ?? doc.Title);
    }

    // ───────────────────────── 드래그 & 드롭 ─────────────────────────

    private void Files_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void Files_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
        {
            if (File.Exists(f)) OpenFileInNewTab(f);
        }
    }

    // ───────────────────────── 파일 작업 ─────────────────────────

    private void OnNew(object? sender, EventArgs e) => NewDocument();

    private void OnOpen(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "모든 파일 (*.*)|*.*|텍스트 파일 (*.txt)|*.txt|서식 있는 텍스트 (*.rtf)|*.rtf|HTML (*.html)|*.html;*.htm",
            FilterIndex = 1,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var f in dlg.FileNames) OpenFileInNewTab(f);
        }
    }

    private void OnSave(object? sender, EventArgs e) => SaveByIndex(_activeDocIndex);

    private void OnSaveAs(object? sender, EventArgs e) => SaveAsByIndex(_activeDocIndex);

    private void OnSaveAll(object? sender, EventArgs e)
    {
        for (int i = 0; i < _documents.Count; i++)
        {
            if (_documents[i].IsModified && !_documents[i].IsLargeFile)
            {
                SwitchToDocument(i);
                SaveByIndex(i);
            }
        }
    }

    private void SaveByIndex(int index)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];
        if (doc.FilePath == null) SaveAsByIndex(index);
        else SaveDocument(index, doc.FilePath);
    }

    private void SaveAsByIndex(int index)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];

        using var dlg = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|서식 있는 텍스트 (*.rtf)|*.rtf|HTML (*.html)|*.html|모든 파일 (*.*)|*.*",
            FilterIndex = 1
        };

        if (doc.FilePath != null)
        {
            dlg.FileName = Path.GetFileName(doc.FilePath);
            string ext = Path.GetExtension(doc.FilePath).ToLowerInvariant();
            dlg.FilterIndex = ext switch
            {
                ".txt" => 1,
                ".rtf" => 2,
                ".html" or ".htm" => 3,
                _ => 4
            };
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
            SaveDocument(index, dlg.FileName);
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
        bool htmlFromRich = (ext == ".html" || ext == ".htm") && !doc.HtmlSource;

        // 텍스트/HTML로 저장하면 이미지·서식이 사라지므로 경고
        if ((ext == ".txt" || htmlFromRich) && DocumentHasImages(doc.Editor))
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
                    doc.IsRich = true;
                    break;
                case ".ymemo":
                    SaveYmemoFrom(doc.Editor, path);
                    doc.IsRich = true;
                    break;
                case ".html":
                case ".htm":
                    if (doc.HtmlSource)
                    {
                        WriteTextDoc(doc, path);
                    }
                    else
                    {
                        string html = RtfHelper.ConvertToHtml(doc.Editor);
                        File.WriteAllText(path, html, new UTF8Encoding(false));
                        doc.IsRich = false;
                    }
                    break;
                default:
                    WriteTextDoc(doc, path);
                    doc.IsRich = false;
                    break;
            }

            doc.FilePath = path;
            doc.Title = Path.GetFileName(path);
            doc.IsModified = false;
            AddRecent(path);
            UpdateTabTitle(index);
            if (index == _activeDocIndex) { UpdateTitle(); UpdateDocMeta(); }
            _statusLabel.Text = $"저장됨: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 저장할 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>문서의 인코딩/줄 바꿈 형식을 유지하여 저장합니다.</summary>
    private void WriteTextDoc(DocumentTab doc, string path)
    {
        var requested = doc.Encoding;
        var used = TextFileHelper.Write(path, doc.Editor!.Text, requested, doc.Eol);
        if (used != requested)
        {
            doc.Encoding = used;
            MessageBox.Show(
                "ANSI(CP949)로 표현할 수 없는 문자가 있어 UTF-8(BOM)으로 저장했습니다.",
                _appTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static bool DocumentHasImages(RichTextBox? editor)
    {
        if (editor == null) return false;
        string rtf = editor.Rtf ?? "";
        return rtf.Contains("\\pict") || rtf.Contains("\\object");
    }

    private void OnCloseTab(object? sender, EventArgs e) => CloseDocument(_activeDocIndex);

    private void OnCloseAll(object? sender, EventArgs e)
    {
        for (int i = _documents.Count - 1; i >= 0; i--)
        {
            if (i >= _documents.Count) continue;
            if (!CloseDocument(i)) break;
        }
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
            if (i >= _documents.Count) continue;
            var doc = _documents[i];
            if (HasUnsavedChanges(doc))
            {
                SwitchToDocument(i);
                bool isNewDoc = string.IsNullOrEmpty(doc.FilePath);
                using var dlg = new CloseConfirmDialog(doc.Title, isNewDoc);
                dlg.ShowDialog(this);

                if (dlg.SelectedAction == CloseConfirmDialog.ConfirmAction.SaveAndClose)
                {
                    SaveByIndex(i);
                    if (HasUnsavedChanges(doc)) { e.Cancel = true; return; }
                }
                else if (dlg.SelectedAction == CloseConfirmDialog.ConfirmAction.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                // DiscardAndClose인 경우 저장하지 않고 다음 문서 확인 계속
            }
        }

        SaveSettings();
        base.OnFormClosing(e);
    }

    // ── 최근 파일 ──

    private void AddRecent(string path)
    {
        _recentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, path);
        while (_recentFiles.Count > 10) _recentFiles.RemoveAt(_recentFiles.Count - 1);
    }

    private void RebuildRecentMenu()
    {
        _recentMenu.DropDownItems.Clear();
        if (_recentFiles.Count == 0)
        {
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(없음)") { Enabled = false });
            return;
        }
        for (int i = 0; i < _recentFiles.Count; i++)
        {
            string p = _recentFiles[i];
            var item = new ToolStripMenuItem($"&{(i + 1) % 10} {p}");
            item.Click += (_, _) =>
            {
                if (File.Exists(p)) OpenFileInNewTab(p);
                else
                {
                    MessageBox.Show("파일을 찾을 수 없습니다:\n" + p, _appTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _recentFiles.Remove(p);
                }
            };
            _recentMenu.DropDownItems.Add(item);
        }
        _recentMenu.DropDownItems.Add(new ToolStripSeparator());
        _recentMenu.DropDownItems.Add("목록 지우기", null, (_, _) => _recentFiles.Clear());
    }

    private void RebuildWindowMenu()
    {
        _windowMenu.DropDownItems.Clear();
        if (_documents.Count == 0)
        {
            _windowMenu.DropDownItems.Add(new ToolStripMenuItem("(문서 없음)") { Enabled = false });
            return;
        }
        for (int i = 0; i < _documents.Count; i++)
        {
            int idx = i;
            var d = _documents[i];
            string name = d.FilePath ?? d.Title;
            var item = new ToolStripMenuItem($"{(i < 9 ? "&" : "")}{i + 1} {name}{(d.IsModified ? " *" : "")}") { Checked = i == _activeDocIndex };
            item.Click += (_, _) => SwitchToDocument(idx);
            _windowMenu.DropDownItems.Add(item);
        }
    }

    // ───────────────────────── 인코딩 / 줄 바꿈 ─────────────────────────

    private bool IsTextDoc(DocumentTab? d) => d != null && !d.IsLargeFile && !d.IsRich && d.Editor != null;

    private void RefreshDocumentMenuState()
    {
        var d = ActiveDoc;
        bool text = IsTextDoc(d);
        foreach (var it in _encItems) { it.Enabled = text; it.Checked = text && (TextEnc)it.Tag! == d!.Encoding; }
        foreach (var it in _eolItems) { it.Enabled = text; it.Checked = text && (EolKind)it.Tag! == d!.Eol; }
    }

    private void SetEncoding(TextEnc enc)
    {
        var d = ActiveDoc;
        if (!IsTextDoc(d) || d!.Encoding == enc) return;
        d.Encoding = enc;
        MarkModified(d);
        UpdateDocMeta();
        _statusLabel.Text = $"저장 인코딩: {TextFileHelper.EncLabel(enc)} (저장 시 적용)";
    }

    private void SetEol(EolKind eol)
    {
        var d = ActiveDoc;
        if (!IsTextDoc(d) || d!.Eol == eol) return;
        d.Eol = eol;
        MarkModified(d);
        UpdateDocMeta();
        _statusLabel.Text = $"줄 바꿈 형식: {TextFileHelper.EolLabel(eol)} (저장 시 적용)";
    }

    private void MarkModified(DocumentTab d)
    {
        if (d.IsModified) return;
        d.IsModified = true;
        int i = _documents.IndexOf(d);
        UpdateTabTitle(i);
        if (d == ActiveDoc) UpdateTitle();
    }

    /// <summary>잘못 감지된 인코딩을 사용자가 지정해서 다시 읽습니다.</summary>
    private void ReopenWithEncoding(TextEnc enc)
    {
        var d = ActiveDoc;
        if (!IsTextDoc(d) || d!.FilePath == null)
        {
            _statusLabel.Text = "저장된 텍스트 파일에서만 사용할 수 있습니다.";
            return;
        }
        if (d.IsModified &&
            MessageBox.Show("저장하지 않은 변경 내용이 사라집니다. 계속하시겠습니까?", _appTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            byte[] bytes = File.ReadAllBytes(d.FilePath);
            int skip = 0;
            if (enc == TextEnc.Utf8 && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                skip = 3;
                enc = TextEnc.Utf8Bom;
            }
            string raw = TextFileHelper.GetEncoding(enc).GetString(bytes, skip, bytes.Length - skip);
            var (text, _, eol) = TextFileHelper.Normalize(raw, enc);

            _suppressModified = true;
            try { d.Editor!.Text = text; }
            finally { _suppressModified = false; }

            d.Encoding = enc;
            d.Eol = eol;
            d.IsModified = false;
            UpdateTabTitle(_activeDocIndex);
            UpdateTitle();
            UpdateDocMeta();
            UpdateWordCount();
            _statusLabel.Text = $"{TextFileHelper.EncLabel(enc)}(으)로 다시 열었습니다.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"다시 열 수 없습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ───────────────────────── 편집 작업 ─────────────────────────

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
            ActiveEditor.Paste();
        }
        else if (Clipboard.ContainsImage())
        {
            PasteClipboardImage();
        }
        else
        {
            ActiveEditor.Paste();
        }
    }

    private void OnSelectAll(object? sender, EventArgs e) => ActiveEditor?.SelectAll();

    private void OnDeleteLine(object? sender, EventArgs e)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        int line = _lineNumberPanel.GetLineFromChar(ed.SelectionStart);
        int start = _lineNumberPanel.GetLineStart(line);
        int len = _lineNumberPanel.GetLineLength(line);
        bool hasNext = line + 1 < _lineNumberPanel.LineCount;
        if (hasNext) len += 1;                    // 개행 포함
        else if (start > 0) { start -= 1; len += 1; } // 마지막 줄이면 앞의 개행을 함께 제거
        ed.Select(start, len);
        ed.SelectedText = "";
    }

    private void OnDuplicateLine(object? sender, EventArgs e)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        int line = _lineNumberPanel.GetLineFromChar(ed.SelectionStart);
        int start = _lineNumberPanel.GetLineStart(line);
        int len = _lineNumberPanel.GetLineLength(line);
        string text = _lineNumberPanel.GetRange(start, len);
        ed.Select(start + len, 0);
        ed.SelectedText = "\n" + text;
    }

    private void OnInsertDateTime(object? sender, EventArgs e)
    {
        if (ActiveEditor != null)
            ActiveEditor.SelectedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    // ───────────────────────── 검색 / 이동 / 북마크 ─────────────────────────

    private FindReplaceForm EnsureFindForm()
    {
        if (_findForm == null || _findForm.IsDisposed)
        {
            _findForm = new FindReplaceForm(() => ActiveEditor);
            _findForm.StatusMessage += m => _statusLabel.Text = m;
        }
        return _findForm;
    }

    private void OnFind(object? sender, EventArgs e) =>
        EnsureFindForm().ShowFind(this, ActiveEditor?.SelectedText ?? "", false);

    private void OnReplace(object? sender, EventArgs e) =>
        EnsureFindForm().ShowFind(this, ActiveEditor?.SelectedText ?? "", true);

    private void OnFindNext(object? sender, EventArgs e)
    {
        var f = EnsureFindForm();
        if (string.IsNullOrEmpty(f.FindText)) { OnFind(sender, e); return; }
        f.FindNext(true);
    }

    private void OnFindPrev(object? sender, EventArgs e)
    {
        var f = EnsureFindForm();
        if (string.IsNullOrEmpty(f.FindText)) { OnFind(sender, e); return; }
        f.FindNext(false);
    }

    private void OnGoToLine(object? sender, EventArgs e)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        int cur = _lineNumberPanel.GetLineFromChar(ed.SelectionStart) + 1;
        using var dlg = new GoToLineDialog(cur, _lineNumberPanel.LineCount);
        if (dlg.ShowDialog(this) == DialogResult.OK) GoToLine(dlg.Line - 1);
    }

    private void GoToLine(int line)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        ed.Select(_lineNumberPanel.GetLineStart(line), 0);
        ed.ScrollToCaret();
        ed.Focus();
    }

    private void OnToggleBookmark(object? sender, EventArgs e)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        _lineNumberPanel.ToggleBookmark(_lineNumberPanel.GetLineFromChar(ed.SelectionStart));
    }

    private void JumpBookmark(bool forward)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        int cur = _lineNumberPanel.GetLineFromChar(ed.SelectionStart);
        int target = _lineNumberPanel.FindBookmark(cur, forward);
        if (target < 0) { _statusLabel.Text = "북마크가 없습니다."; return; }
        GoToLine(target);
        _statusLabel.Text = $"북마크: {target + 1}줄";
    }

    private void OnNextBookmark(object? sender, EventArgs e) => JumpBookmark(true);
    private void OnPrevBookmark(object? sender, EventArgs e) => JumpBookmark(false);
    private void OnClearBookmarks(object? sender, EventArgs e) => _lineNumberPanel.ClearBookmarks();

    // ───────────────────────── 에디터 이벤트 ─────────────────────────

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressModified || sender is not RichTextBox rtb) return;
        var doc = _documents.FirstOrDefault(d => d.Editor == rtb);
        if (doc == null) return;

        if (!doc.IsModified)
        {
            doc.IsModified = true;
            UpdateTabTitle(_documents.IndexOf(doc));
            if (doc == ActiveDoc) UpdateTitle();
        }

        if (doc == ActiveDoc)
        {
            // 단어 수 계산은 O(n)이므로 입력이 멈춘 뒤에만 수행
            _countTimer.Stop();
            _countTimer.Start();
        }
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

    private static readonly Regex UrlRegex = new(@"(?:https?|ftp)://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Ctrl+클릭: 삽입한 하이퍼링크 또는 본문의 http(s):// 주소를 엽니다.</summary>
    private void Editor_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || (ModifierKeys & Keys.Control) == 0) return;
        if (sender is not RichTextBox rtb || rtb != ActiveEditor) return;

        int idx = rtb.GetCharIndexFromPosition(e.Location);
        if (idx < 0 || idx >= rtb.TextLength) return;

        if (RtfHelper.TryOpenHyperlink(rtb, idx, idx + 1)) return;

        int line = _lineNumberPanel.GetLineFromChar(idx);
        int start = _lineNumberPanel.GetLineStart(line);
        string text = _lineNumberPanel.GetRange(start, _lineNumberPanel.GetLineLength(line));
        foreach (Match m in UrlRegex.Matches(text))
        {
            int abs = start + m.Index;
            if (idx >= abs && idx <= abs + m.Length)
            {
                try { Process.Start(new ProcessStartInfo(m.Value.TrimEnd('.', ',', ')', ';')) { UseShellExecute = true }); } catch { }
                return;
            }
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // 창 크기가 확정된 뒤에 최소 크기와 분할 위치를 지정해야 SplitContainer가 예외를 던지지 않습니다.
        try
        {
            if (!_splitContainer.Panel1Collapsed && _splitContainer.Width > S(400))
            {
                _splitContainer.Panel1MinSize = S(100);
                _splitContainer.Panel2MinSize = S(200);
                int maxDist = _splitContainer.Width - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
                int want = _pendingSplitterDistance > 0 ? _pendingSplitterDistance : S(200);
                _splitContainer.SplitterDistance = Math.Clamp(want, S(100), Math.Max(S(100), Math.Min(maxDist, S(600))));
            }
        }
        catch (Exception) { /* 분할 위치 복원 실패 시 기본값 유지 */ }
        _lastSplitter = _splitContainer.SplitterDistance;
        SyncRuler();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F1) { OnAbout(null, EventArgs.Empty); e.Handled = true; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Tab)) { CycleDocument(+1); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)) { CycleDocument(-1); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateFormatToolbarState()
    {
        if (ActiveEditor == null || _updatingToolbar) return;
        _updatingToolbar = true;   // 프로그램이 콤보 값을 바꿀 때 서식이 다시 적용되어 '수정됨'으로 바뀌는 것을 방지
        try
        {
            var font = ActiveEditor.SelectionFont;
            if (font != null)
            {
                _boldBtn.Checked = font.Bold;
                _italicBtn.Checked = font.Italic;
                _underlineBtn.Checked = font.Underline;
                _strikeBtn.Checked = font.Strikeout;
                _fontSizeCombo.Text = font.Size.ToString("0.#");

                int i = _fontFamilyCombo.Items.IndexOf(font.FontFamily.Name);
                if (i >= 0 && _fontFamilyCombo.SelectedIndex != i) _fontFamilyCombo.SelectedIndex = i;
            }
            _alignLeftBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Left;
            _alignCenterBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Center;
            _alignRightBtn.Checked = ActiveEditor.SelectionAlignment == HorizontalAlignment.Right;
            _bulletListBtn.Checked = ActiveEditor.SelectionBullet;
        }
        finally { _updatingToolbar = false; }
    }

    // ───────────────────────── 상태 표시줄 / 제목 ─────────────────────────

    /// <summary>전각(한글/CJK) 문자는 2칸으로 계산합니다 (EditPlus와 동일한 방식).</summary>
    private static int CellWidth(char c) =>
        (c >= 0x1100 && c <= 0x115F) || (c >= 0x2E80 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3) ||
        (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE30 && c <= 0xFE6F) || (c >= 0xFF00 && c <= 0xFF60) ||
        (c >= 0xFFE0 && c <= 0xFFE6) ? 2 : 1;

    private void UpdateCursorPosition()
    {
        if (ActiveDoc != null && ActiveDoc.IsLargeFile)
        {
            var viewer = ActiveDoc.LargeViewer!;
            _lineColLabel.Text = $"줄 {viewer.CurrentLine + 1} / {viewer.TotalLines:N0}";
            return;
        }
        var ed = ActiveEditor;
        if (ed == null) return;

        int idx = ed.SelectionStart;
        int line = _lineNumberPanel.GetLineFromChar(idx);
        int start = _lineNumberPanel.GetLineStart(line);
        string prefix = _lineNumberPanel.GetRange(start, idx - start);
        int col = 1;
        foreach (char c in prefix) col += c == '\t' ? 1 : CellWidth(c);

        string hex;
        var ch = _lineNumberPanel.GetCharAt(idx);
        if (!ch.HasValue) hex = "00";
        else if (ch.Value == '\n') hex = ActiveDoc!.Eol == EolKind.Unix ? "0A" : "0D";
        else hex = ch.Value > 0xFF ? ((int)ch.Value).ToString("X4") : ((int)ch.Value).ToString("X2");

        _lineColLabel.Text = $"줄 {line + 1}  칸 {col}  {hex}";
    }

    private void UpdateDocMeta()
    {
        var d = ActiveDoc;
        if (d == null) return;
        if (d.IsLargeFile)
        {
            _eolLabel.Text = "";
            _encodingLabel.Text = TextFileHelper.EncLabel(d.Encoding);
        }
        else if (d.IsRich)
        {
            _eolLabel.Text = "";
            _encodingLabel.Text = "RTF";
        }
        else
        {
            _eolLabel.Text = TextFileHelper.EolLabel(d.Eol);
            _encodingLabel.Text = TextFileHelper.EncLabel(d.Encoding);
        }
    }

    private void UpdateTitle()
    {
        if (ActiveDoc == null) { Text = _appName; return; }
        string name = ActiveDoc.FilePath ?? ActiveDoc.Title;
        Text = $"{_appName} - [{name}{(ActiveDoc.IsModified ? " *" : "")}]";
    }

    private void UpdateWordCount()
    {
        if (ActiveDoc != null && ActiveDoc.IsLargeFile)
        {
            _wordCountLabel.Text = $"{ActiveDoc.LargeViewer!.TotalLines:N0} 줄 (대용량)";
            return;
        }
        var ed = ActiveEditor;
        if (ed == null) { _wordCountLabel.Text = ""; return; }

        string text = ed.Text;
        int words = 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) inWord = false;
            else if (!inWord) { inWord = true; words++; }
        }
        _wordCountLabel.Text = $"{words:N0} 단어 | {text.Length:N0} 자";
    }

    // ───────────────────────── 도움말 ─────────────────────────

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "윤슬 에디터 v1.2\n\n" +
            "EditPlus 스타일 UI의 위지위그(WYSIWYG) 멀티탭 텍스트 편집기\n\n" +
            "주요 기능:\n" +
            "• 상단 문서 탭 / 눈금자 / 줄 번호 / 북마크 (Ctrl+F2, F2)\n" +
            "• 좌측 디렉토리 창 + 클립텍스트\n" +
            "• ANSI/UTF-8/UTF-16 자동 감지, PC/UNIX/MAC 줄 바꿈 유지\n" +
            "• 찾기/바꾸기 (대소문자, 단어 단위, 정규식), 줄 이동 (Ctrl+G)\n" +
            "• 굵게/기울임/밑줄/취소선, 글꼴·색상, 정렬, 글머리 기호\n" +
            "• 이미지 삽입 (파일/클립보드) - 원본 크기\n" +
            "• 하이퍼링크 / URL Ctrl+클릭으로 열기\n" +
            "• 인쇄 미리보기, 5MB 이상 대용량 텍스트 읽기 모드\n" +
            "• 파일 끌어다 놓기로 열기\n\n" +
            "지원 형식: .txt, .rtf, .html 외 모든 텍스트 파일",
            "윤슬 에디터 정보",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ───────────────────────── 설정 저장/복원 ─────────────────────────

    private static string EscapeClip(string s) => s.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n");

    private static string UnescapeClip(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n);
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private void SaveSettings()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var lines = new List<string>
            {
                $"WindowWidth={b.Width}",
                $"WindowHeight={b.Height}",
                $"WindowLeft={b.Left}",
                $"WindowTop={b.Top}",
                $"WindowState={(WindowState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal)}",
                $"ShowRuler={(_showRuler ? 1 : 0)}",
                $"ShowLineNumbers={(_showLineNumbers ? 1 : 0)}",
                $"ShowDirPane={(_showDirPane ? 1 : 0)}",
                $"ShowStatusBar={(_showStatusBar ? 1 : 0)}",
                $"ShowMainTool={(_showMainTool ? 1 : 0)}",
                $"ShowFormatTool={(_showFormatTool ? 1 : 0)}",
                $"WordWrap={(_wordWrap ? 1 : 0)}",
                $"EditorFont={_editorFont.Name}|{_editorFont.Size.ToString(CultureInfo.InvariantCulture)}"
            };
            if (_lastSplitter > 0) lines.Add($"SplitterDistance={_lastSplitter}");
            for (int i = 0; i < _recentFiles.Count; i++) lines.Add($"Recent{i}={_recentFiles[i]}");
            lines.Add($"ClipCount={_clipItems.Count}");
            for (int i = 0; i < _clipItems.Count; i++) lines.Add($"Clip{i}={EscapeClip(_clipItems[i])}");

            File.WriteAllLines(_settingsPath, lines, new UTF8Encoding(false));
        }
        catch { }
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var s = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(_settingsPath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) s[line[..eq]] = line[(eq + 1)..];
            }

            bool Flag(string key, bool def) => s.TryGetValue(key, out var v) ? v == "1" : def;
            int Num(string key, int def) => s.TryGetValue(key, out var v) && int.TryParse(v, out int n) ? n : def;

            _showRuler = Flag("ShowRuler", _showRuler);
            _showLineNumbers = Flag("ShowLineNumbers", _showLineNumbers);
            _showDirPane = Flag("ShowDirPane", _showDirPane);
            _showStatusBar = Flag("ShowStatusBar", _showStatusBar);
            _showMainTool = Flag("ShowMainTool", _showMainTool);
            _showFormatTool = Flag("ShowFormatTool", _showFormatTool);
            _wordWrap = Flag("WordWrap", _wordWrap);

            if (s.TryGetValue("EditorFont", out var ef))
            {
                var parts = ef.Split('|');
                if (parts.Length == 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sz) && sz >= 6 && sz <= 72)
                {
                    try { _editorFont = new Font(parts[0], sz); } catch { }
                }
            }

            if (s.TryGetValue("WindowState", out var stateStr) && Enum.TryParse<FormWindowState>(stateStr, out var state))
                _pendingState = state;
            if (s.ContainsKey("WindowWidth") && s.ContainsKey("WindowHeight") && s.ContainsKey("WindowLeft") && s.ContainsKey("WindowTop"))
                _pendingBounds = new Rectangle(Num("WindowLeft", 0), Num("WindowTop", 0), Math.Max(400, Num("WindowWidth", 1100)), Math.Max(300, Num("WindowHeight", 750)));

            _pendingSplitterDistance = Num("SplitterDistance", -1);

            for (int i = 0; i < 10; i++)
                if (s.TryGetValue($"Recent{i}", out var rp) && rp.Length > 0) _recentFiles.Add(rp);

            if (s.ContainsKey("ClipCount"))
            {
                int n = Num("ClipCount", 0);
                for (int i = 0; i < n; i++)
                    if (s.TryGetValue($"Clip{i}", out var cv)) _clipItems.Add(UnescapeClip(cv));
                _clipLoaded = true;
            }
        }
        catch { }
    }

    private void ApplyWindowSettings()
    {
        if (_pendingState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
        else if (_pendingBounds is Rectangle b)
        {
            // 모니터 구성이 바뀌어 창이 화면 밖에 있으면 복원하지 않고 가운데에 띄움
            var probe = new Rectangle(b.Left, b.Top, Math.Min(b.Width, 200), Math.Min(b.Height, 100));
            if (Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(probe)))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = b;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countTimer.Dispose();
            _findForm?.Dispose();
        }
        base.Dispose(disposing);
    }

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
            UpdateColorIcons();
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
            UpdateColorIcons();
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

    private void FontFamily_Changed(object? sender, EventArgs e)
    {
        if (_updatingToolbar || ActiveEditor == null || _fontFamilyCombo.SelectedItem == null) return;
        string fontName = _fontFamilyCombo.SelectedItem.ToString()!;
        var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
        try { ApplySelectionFont(new Font(fontName, currentFont.Size, currentFont.Style)); } catch { }
    }

    private void FontSize_Changed(object? sender, EventArgs e) { if (!_updatingToolbar) ApplyFontSize(); }
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
        if (_updatingToolbar || ActiveEditor == null || _paragraphCombo.SelectedIndex < 0) return;
        var currentFont = ActiveEditor.SelectionFont ?? ActiveEditor.Font;
        var (size, style) = _paragraphCombo.SelectedIndex switch
        {
            1 => (24f, FontStyle.Bold),
            2 => (18f, FontStyle.Bold),
            3 => (14f, FontStyle.Bold),
            _ => (_editorFont.Size, FontStyle.Regular)
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
        OnInsertOrEditTable(forceEdit: false);
    }

    private void OnInsertOrEditTable(bool forceEdit = false)
    {
        if (ActiveEditor == null) return;

        bool inTable = TableHelper.IsCursorInTable(ActiveEditor);
        if (inTable || forceEdit)
        {
            // 커서가 표 내부에 있으면 표 서식 및 속성 변경 모드
            if (TableHelper.TrySelectTableAtCursor(ActiveEditor, out _))
            {
                string rtf = ActiveEditor.SelectedRtf ?? "";
                string[,]? cellData = TableHelper.ParseTableCells(rtf, out int oldRows, out int oldCols);

                var initOpt = new TableStyleOptions
                {
                    Rows = Math.Max(1, oldRows),
                    Cols = Math.Max(1, oldCols)
                };

                using var dlg = new TableDialog(isModifyMode: true, initialOptions: initOpt);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    TableHelper.InsertTable(ActiveEditor, dlg.Options, cellData);
                    string borderDesc = dlg.IsTransparentBorder ? "투명선" : $"{dlg.BorderWidth}pt";
                    _statusLabel.Text = $"표 서식 변경 완료: {dlg.Rows}행 × {dlg.Cols}열 (선: {borderDesc})";
                }
                return;
            }
        }

        // 신규 표 삽입 모드
        using var newDlg = new TableDialog(isModifyMode: false);
        if (newDlg.ShowDialog(this) == DialogResult.OK)
        {
            TableHelper.InsertTable(ActiveEditor, newDlg.Options);
            string borderDesc = newDlg.IsTransparentBorder ? "투명선" : $"{newDlg.BorderWidth}pt";
            _statusLabel.Text = $"멋진 표 삽입 완료: {newDlg.Rows}행 × {newDlg.Cols}열 (선: {borderDesc})";
        }
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

    /// <summary>저장할 인코딩 (열 때 자동 감지한 값 유지)</summary>
    public TextEnc Encoding { get; set; } = TextEnc.Utf8Bom;

    /// <summary>저장할 줄 바꿈 형식 (열 때 감지한 값 유지)</summary>
    public EolKind Eol { get; set; } = EolKind.Pc;

    /// <summary>RTF/ymemo 등 서식 있는 문서 여부</summary>
    public bool IsRich { get; set; }

    /// <summary>.html/.htm 파일을 소스 그대로 열어 편집 중인지 (true면 저장 시 변환 없이 원문 저장)</summary>
    public bool HtmlSource { get; set; }

    /// <summary>북마크한 줄 번호 (0-based)</summary>
    public HashSet<int> Bookmarks { get; } = new();
}
