using System.Text;

namespace YunseulMemo;

/// <summary>
/// 대용량 텍스트 파일을 가상 스크롤 방식으로 표시하는 컨트롤.
/// 파일 전체를 메모리에 올리지 않고, 줄 오프셋 인덱스를 만든 뒤
/// 화면에 보이는 줄만 읽어서 그립니다.
/// </summary>
public class LargeFileViewer : Control
{
    private string? _filePath;
    private List<long> _lineOffsets = new(); // 각 줄의 바이트 오프셋
    private int _totalLines;
    private int _topLine; // 현재 화면 최상단 줄 번호 (0-based)
    private int _visibleLines; // 화면에 보이는 줄 수

    private VScrollBar _scrollBar = null!;
    private Font _font;
    private readonly Font _markerFont;
    private Brush _textBrush = Brushes.Black;
    private Brush _lineNumBrush = new SolidBrush(Color.FromArgb(120, 120, 120));
    private Brush _currentLineBrush = new SolidBrush(Color.FromArgb(30, 30, 140));
    private Brush _bgBrush = Brushes.White;
    private Brush _lineNumBg = new SolidBrush(Color.FromArgb(238, 238, 238));
    private Pen _borderPen = new Pen(Color.FromArgb(170, 170, 170));

    private int _lineHeight;
    private int _lineNumWidth = 60;
    private int _currentLine; // 클릭으로 선택한 줄
    private TextEnc _enc = TextEnc.Utf8;
    private Encoding _encoding = new UTF8Encoding(false);

    // 캐시: 최근 읽은 줄 블록
    private int _cacheStart = -1;
    private string[] _cacheLines = Array.Empty<string>();
    private const int CacheBlockSize = 500;

    public int TotalLines => _totalLines;
    /// <summary>파일 앞부분으로 감지한 인코딩 (BOM → 유효한 UTF-8 → ANSI).</summary>
    public TextEnc DetectedEncoding => _enc;
    public int CurrentLine => _currentLine;
    public bool IsLoaded => _filePath != null && _totalLines > 0;

    public LargeFileViewer()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        _font = new Font("Consolas", 10f);
        _markerFont = new Font("맑은 고딕", 7f);
        _lineHeight = _font.Height + 2;
        BackColor = Color.White;
        TabStop = true;

        _scrollBar = new VScrollBar
        {
            Dock = DockStyle.Right,
            Minimum = 0,
            LargeChange = 10,
            SmallChange = 1
        };
        _scrollBar.ValueChanged += ScrollBar_ValueChanged;
        Controls.Add(_scrollBar);

        Resize += (s, e) => UpdateLayout();
    }

    /// <summary>
    /// 대용량 파일을 인덱싱하여 열기.
    /// 줄 오프셋만 스캔하므로 빠르게 완료됩니다.
    /// </summary>
    public void LoadFile(string path)
    {
        _filePath = path;
        _lineOffsets.Clear();
        _topLine = 0;
        _currentLine = 0;
        _cacheStart = -1;
        _cacheLines = Array.Empty<string>();

        // 인코딩 감지 (BOM 확인)
        _enc = TextFileHelper.DetectEncoding(path);
        _encoding = TextFileHelper.GetEncoding(_enc);

        // 줄 오프셋 인덱싱
        IndexLines(path);

        _totalLines = _lineOffsets.Count;
        UpdateLayout();
        Invalidate();
    }

    private void IndexLines(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);

        // BOM 건너뛰기
        int bomSize = GetBomSize(_enc);
        if (bomSize > 0) fs.Seek(bomSize, SeekOrigin.Begin);

        _lineOffsets.Add(fs.Position);

        byte[] buffer = new byte[1024 * 1024]; // 1MB 버퍼
        int bytesRead;
        long position = fs.Position;

        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    _lineOffsets.Add(position + i + 1 + (_enc == TextEnc.Utf16Le ? 1 : 0));
                }
            }
            position += bytesRead;
        }

        // 마지막 줄이 빈 줄이면 제거
        if (_lineOffsets.Count > 1 && _lineOffsets[^1] >= new FileInfo(path).Length)
            _lineOffsets.RemoveAt(_lineOffsets.Count - 1);
    }

    private static int GetBomSize(TextEnc enc) => enc switch
    {
        TextEnc.Utf8Bom => 3,
        TextEnc.Utf16Le or TextEnc.Utf16Be => 2,
        _ => 0
    };

    /// <summary>
    /// 지정된 줄 범위의 텍스트를 파일에서 읽어옵니다.
    /// </summary>
    private string[] ReadLines(int startLine, int count)
    {
        if (_filePath == null || startLine < 0) return Array.Empty<string>();

        // 캐시 확인
        if (_cacheStart >= 0 && startLine >= _cacheStart && startLine + count <= _cacheStart + _cacheLines.Length)
        {
            int offset = startLine - _cacheStart;
            return _cacheLines.Skip(offset).Take(count).ToArray();
        }

        // 캐시 미스: 블록 단위로 읽기
        int blockStart = Math.Max(0, startLine - CacheBlockSize / 4);
        int blockEnd = Math.Min(_totalLines, blockStart + CacheBlockSize);

        var lines = new List<string>();
        try
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);

            for (int i = blockStart; i < blockEnd; i++)
            {
                long lineStart = _lineOffsets[i];
                long lineEnd = (i + 1 < _totalLines) ? _lineOffsets[i + 1] : fs.Length;
                int length = (int)Math.Min(lineEnd - lineStart, 10000); // 한 줄 최대 10000바이트

                fs.Seek(lineStart, SeekOrigin.Begin);
                byte[] buf = new byte[length];
                int read = fs.Read(buf, 0, length);

                string line = _encoding.GetString(buf, 0, read).TrimEnd('\r', '\n');
                lines.Add(line);
            }
        }
        catch
        {
            return Array.Empty<string>();
        }

        _cacheStart = blockStart;
        _cacheLines = lines.ToArray();

        int resultOffset = startLine - blockStart;
        return _cacheLines.Skip(resultOffset).Take(count).ToArray();
    }

    private void UpdateLayout()
    {
        _visibleLines = Math.Max(1, (ClientSize.Height - 4) / _lineHeight);
        _scrollBar.Maximum = Math.Max(0, _totalLines - 1);
        _scrollBar.LargeChange = Math.Max(1, _visibleLines - 2);
        Invalidate();
    }

    private void ScrollBar_ValueChanged(object? sender, EventArgs e)
    {
        _topLine = _scrollBar.Value;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int delta = e.Delta > 0 ? -3 : 3;
        _topLine = Math.Clamp(_topLine + delta, 0, Math.Max(0, _totalLines - _visibleLines));
        _scrollBar.Value = _topLine;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        int clickedLine = _topLine + (e.Y / _lineHeight);
        if (clickedLine >= 0 && clickedLine < _totalLines)
        {
            _currentLine = clickedLine;
            Invalidate();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Up:
                if (_currentLine > 0) _currentLine--;
                EnsureVisible(_currentLine);
                break;
            case Keys.Down:
                if (_currentLine < _totalLines - 1) _currentLine++;
                EnsureVisible(_currentLine);
                break;
            case Keys.PageUp:
                _currentLine = Math.Max(0, _currentLine - _visibleLines);
                EnsureVisible(_currentLine);
                break;
            case Keys.PageDown:
                _currentLine = Math.Min(_totalLines - 1, _currentLine + _visibleLines);
                EnsureVisible(_currentLine);
                break;
            case Keys.Home:
                if (e.Control) { _currentLine = 0; EnsureVisible(0); }
                break;
            case Keys.End:
                if (e.Control) { _currentLine = _totalLines - 1; EnsureVisible(_currentLine); }
                break;
        }
        Invalidate();
    }

    private void EnsureVisible(int line)
    {
        if (line < _topLine)
            _topLine = line;
        else if (line >= _topLine + _visibleLines)
            _topLine = line - _visibleLines + 1;

        _topLine = Math.Clamp(_topLine, 0, Math.Max(0, _totalLines - _visibleLines));
        _scrollBar.Value = _topLine;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.White);

        if (!IsLoaded) return;

        // 줄 번호 영역 배경
        g.FillRectangle(_lineNumBg, 0, 0, _lineNumWidth, Height);
        g.DrawLine(_borderPen, _lineNumWidth, 0, _lineNumWidth, Height);

        // 보이는 줄 읽기
        int count = Math.Min(_visibleLines + 1, _totalLines - _topLine);
        string[] lines = ReadLines(_topLine, count);

        for (int i = 0; i < lines.Length; i++)
        {
            int lineIndex = _topLine + i;
            int y = i * _lineHeight + 2;

            // 현재 줄 하이라이트
            if (lineIndex == _currentLine)
            {
                using var hlBrush = new SolidBrush(Color.FromArgb(232, 242, 255));
                g.FillRectangle(hlBrush, _lineNumWidth + 1, y - 1, ClientSize.Width - _lineNumWidth - _scrollBar.Width, _lineHeight);
            }

            // 줄 번호
            string numStr = (lineIndex + 1).ToString();
            var numBrush = (lineIndex == _currentLine) ? _currentLineBrush : _lineNumBrush;
            var numSize = g.MeasureString(numStr, _font);
            g.DrawString(numStr, _font, numBrush, _lineNumWidth - numSize.Width - 4, y);

            // 현재 줄 마커
            if (lineIndex == _currentLine)
                g.DrawString("▶", _markerFont, _currentLineBrush, 2, y + 2);

            // 텍스트
            g.DrawString(lines[i], _font, _textBrush, _lineNumWidth + 6, y);
        }

        // 상태 정보 (우하단)
        string info = $"줄 {_currentLine + 1} / {_totalLines:N0}";
        var infoSize = g.MeasureString(info, Font);
        g.DrawString(info, Font, _lineNumBrush,
            ClientSize.Width - _scrollBar.Width - infoSize.Width - 8,
            ClientSize.Height - infoSize.Height - 4);
    }

    /// <summary>
    /// 특정 줄의 텍스트를 반환합니다.
    /// </summary>
    public string GetLineText(int lineIndex)
    {
        var lines = ReadLines(lineIndex, 1);
        return lines.Length > 0 ? lines[0] : "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _markerFont.Dispose();
            _lineNumBrush.Dispose();
            _currentLineBrush.Dispose();
            _lineNumBg.Dispose();
            _borderPen.Dispose();
        }
        base.Dispose(disposing);
    }
}
