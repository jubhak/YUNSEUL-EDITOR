using System.Text;

namespace YunseulMemo;

/// <summary>문서 인코딩 (EditPlus의 ANSI / UTF-8 / UTF-8 BOM / UTF-16 구분).</summary>
public enum TextEnc { Ansi, Utf8, Utf8Bom, Utf16Le, Utf16Be }

/// <summary>줄 바꿈 형식 (EditPlus의 PC / UNIX / MAC).</summary>
public enum EolKind { Pc, Unix, Mac }

/// <summary>
/// 텍스트 파일 읽기/쓰기 유틸리티.
/// - 인코딩 자동 감지 (BOM → 유효한 UTF-8 → 그 외 ANSI(CP949))
/// - 줄 바꿈 형식 감지 및 저장 시 원래 형식 유지
/// RichTextBox는 내부적으로 '\n'만 쓰므로 읽을 때 정규화하고, 저장할 때 원래 형식으로 되돌립니다.
/// </summary>
internal static class TextFileHelper
{
    private static Encoding? _ansi;

    public static Encoding AnsiEncoding
    {
        get
        {
            if (_ansi != null) return _ansi;
            try { _ansi = Encoding.GetEncoding(949); }
            catch { _ansi = Encoding.Default; }
            return _ansi;
        }
    }

    public static string EncLabel(TextEnc e) => e switch
    {
        TextEnc.Ansi => "ANSI",
        TextEnc.Utf8 => "UTF-8",
        TextEnc.Utf8Bom => "UTF-8 BOM",
        TextEnc.Utf16Le => "UTF-16 LE",
        TextEnc.Utf16Be => "UTF-16 BE",
        _ => "?"
    };

    public static string EolLabel(EolKind e) => e switch
    {
        EolKind.Unix => "UNIX",
        EolKind.Mac => "MAC",
        _ => "PC"
    };

    public static string EolString(EolKind e) => e switch
    {
        EolKind.Unix => "\n",
        EolKind.Mac => "\r",
        _ => "\r\n"
    };

    public static Encoding GetEncoding(TextEnc e) => e switch
    {
        TextEnc.Ansi => AnsiEncoding,
        TextEnc.Utf8 => new UTF8Encoding(false),
        TextEnc.Utf8Bom => new UTF8Encoding(true),
        TextEnc.Utf16Le => new UnicodeEncoding(false, true),
        TextEnc.Utf16Be => new UnicodeEncoding(true, true),
        _ => new UTF8Encoding(false)
    };

    /// <summary>
    /// 파일의 처음/중간/끝 구간(각 최대 64KB)을 샘플링해 인코딩을 추정합니다. 대용량 파일 뷰어에서도 사용합니다.
    /// 앞부분이 영문뿐이고 한글은 뒤쪽에만 있는 ANSI 로그도 올바르게 감지하기 위해 여러 구간을 확인합니다.
    /// </summary>
    public static TextEnc DetectEncoding(string path)
    {
        const int Chunk = 64 * 1024;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buf = new byte[Chunk];
        int n = fs.Read(buf, 0, buf.Length);
        var enc = Detect(buf, n, fs.Length > n);
        if (enc != TextEnc.Utf8 || fs.Length <= Chunk) return enc;

        // 앞부분이 UTF-8로 유효하면 중간/끝 구간도 확인 (구간이 멀티바이트 문자 중간에서 시작할 수 있으므로 연속 바이트는 건너뜀)
        foreach (long pos in new[] { fs.Length / 2, Math.Max(0, fs.Length - Chunk) })
        {
            fs.Seek(pos, SeekOrigin.Begin);
            int m = fs.Read(buf, 0, buf.Length);
            int skip = 0;
            while (skip < m && skip < 3 && (buf[skip] & 0xC0) == 0x80) skip++;
            if (skip > 0) Array.Copy(buf, skip, buf, 0, m - skip);
            m -= skip;
            bool more = pos + skip + m < fs.Length;
            if (!IsValidUtf8(buf, m, more)) return TextEnc.Ansi;
        }
        return TextEnc.Utf8;
    }

    private static TextEnc Detect(byte[] b, int n, bool truncated)
    {
        if (n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return TextEnc.Utf8Bom;
        if (n >= 2 && b[0] == 0xFF && b[1] == 0xFE) return TextEnc.Utf16Le;
        if (n >= 2 && b[0] == 0xFE && b[1] == 0xFF) return TextEnc.Utf16Be;
        if (IsValidUtf8(b, n, truncated)) return TextEnc.Utf8;
        return TextEnc.Ansi;
    }

    private static bool IsValidUtf8(byte[] b, int n, bool truncated)
    {
        try
        {
            var dec = new UTF8Encoding(false, true).GetDecoder();
            // flush=false: 버퍼 끝에서 잘린 멀티바이트 시퀀스는 오류로 보지 않음
            dec.GetCharCount(b, 0, n, !truncated);
            return true;
        }
        catch (DecoderFallbackException) { return false; }
    }

    public static (string Text, TextEnc Enc, EolKind Eol) Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var enc = Detect(bytes, Math.Min(bytes.Length, 64 * 1024), bytes.Length > 64 * 1024);

        // 앞부분만 검사했으므로, 전체가 UTF-8이 아니면 ANSI로 보정
        if (enc == TextEnc.Utf8 && bytes.Length > 64 * 1024 && !IsValidUtf8(bytes, bytes.Length, false))
            enc = TextEnc.Ansi;

        int skip = enc switch { TextEnc.Utf8Bom => 3, TextEnc.Utf16Le or TextEnc.Utf16Be => 2, _ => 0 };
        string text = GetEncoding(enc).GetString(bytes, skip, bytes.Length - skip);
        return Normalize(text, enc);
    }

    public static (string Text, TextEnc Enc, EolKind Eol) Normalize(string text, TextEnc enc)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                else cr++;
            }
            else if (c == '\n') lf++;
        }

        var eol = EolKind.Pc;
        if (lf > crlf && lf >= cr) eol = EolKind.Unix;
        else if (cr > crlf && cr > lf) eol = EolKind.Mac;

        if (crlf + lf + cr > 0)
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return (text, enc, eol);
    }

    /// <summary>
    /// 에디터 텍스트('\n' 줄바꿈)를 지정한 인코딩/줄바꿈으로 저장합니다.
    /// ANSI로 표현할 수 없는 문자가 있으면 UTF-8(BOM)로 자동 전환하고 실제 사용한 인코딩을 반환합니다.
    /// </summary>
    public static TextEnc Write(string path, string text, TextEnc enc, EolKind eol)
    {
        string eolStr = EolString(eol);
        string content = eolStr == "\n" ? text : text.Replace("\n", eolStr);

        if (enc == TextEnc.Ansi)
        {
            var ansi = AnsiEncoding;
            string roundTrip = ansi.GetString(ansi.GetBytes(content));
            if (!string.Equals(roundTrip, content, StringComparison.Ordinal))
                enc = TextEnc.Utf8Bom;
        }

        var encoding = GetEncoding(enc);
        var body = encoding.GetBytes(content);
        var pre = encoding.GetPreamble();
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        if (pre.Length > 0) fs.Write(pre, 0, pre.Length);
        fs.Write(body, 0, body.Length);
        return enc;
    }
}
