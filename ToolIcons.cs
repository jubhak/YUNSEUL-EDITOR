using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace YunseulMemo;

internal enum Ico
{
    New, Open, Save, SaveAll, Print, Cut, Copy, Paste, Undo, Redo,
    Find, Replace, GoTo, WordWrap, LineNumber, Ruler, Bookmark,
    Bold, Italic, Underline, Strike, TextColor, BackColor,
    AlignLeft, AlignCenter, AlignRight, Bullet, Indent, Outdent,
    Image, ClipImage, Link, HRule, Table, Page, Folder, FolderOpen
}

/// <summary>
/// EditPlus 풍의 16x16 클래식 아이콘을 GDI+로 직접 그립니다.
/// 16x16 논리 좌표계로 그린 뒤 요청한 크기로 스케일하므로 어떤 DPI에서도 선명합니다.
/// </summary>
internal static class ToolIcons
{
    private static readonly Color Outline = Color.FromArgb(70, 70, 80);
    private static readonly Color Yellow = Color.FromArgb(244, 204, 86);
    private static readonly Color YellowLt = Color.FromArgb(255, 232, 140);
    private static readonly Color YellowDk = Color.FromArgb(160, 120, 20);
    private static readonly Color Blue = Color.FromArgb(40, 90, 190);
    private static readonly Color BlueLt = Color.FromArgb(150, 190, 245);

    public static Bitmap Create(Ico kind, int size, Color? accent = null)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.ScaleTransform(size / 16f, size / 16f);
        Draw(g, kind, accent);
        return bmp;
    }

    private static Pen P(Color c, float w = 1f) => new Pen(c, w) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
    private static SolidBrush B(Color c) => new SolidBrush(c);

    private static void Page(Graphics g, float x, float y, float w, float h, Color? band = null)
    {
        float f = 4f;
        var pts = new[] { new PointF(x, y), new PointF(x + w - f, y), new PointF(x + w, y + f), new PointF(x + w, y + h), new PointF(x, y + h) };
        using (var br = B(Color.White)) g.FillPolygon(br, pts);
        using (var pen = P(Outline)) g.DrawPolygon(pen, pts);
        using (var pen = P(Outline)) g.DrawLines(pen, new[] { new PointF(x + w - f, y), new PointF(x + w - f, y + f), new PointF(x + w, y + f) });
        if (band.HasValue)
            using (var br = B(band.Value)) g.FillRectangle(br, x + 1.5f, y + h - 4.5f, w - 3f, 3f);
        else
            using (var pen = P(Color.FromArgb(150, 150, 160), 1f))
                for (int i = 0; i < 3; i++) g.DrawLine(pen, x + 2, y + 6 + i * 2.5f, x + w - 2, y + 6 + i * 2.5f);
    }

    private static void Floppy(Graphics g, float ox, float oy, float s)
    {
        g.TranslateTransform(ox, oy); g.ScaleTransform(s, s);
        var body = new[] { new PointF(1, 1), new PointF(13, 1), new PointF(15, 3), new PointF(15, 15), new PointF(1, 15) };
        using (var br = B(Blue)) g.FillPolygon(br, body);
        using (var br = B(Color.FromArgb(215, 215, 225))) g.FillRectangle(br, 4, 1, 8, 5);
        using (var br = B(Blue)) g.FillRectangle(br, 9, 2, 2, 3);
        using (var br = B(Color.White)) g.FillRectangle(br, 3, 9, 10, 6);
        using (var pen = P(Color.FromArgb(150, 150, 170), 0.8f)) { g.DrawLine(pen, 4.5f, 11, 11.5f, 11); g.DrawLine(pen, 4.5f, 13, 11.5f, 13); }
        g.ResetTransform();
    }

    private static void Magnifier(Graphics g, float cx, float cy, float r)
    {
        using (var pen = P(Color.FromArgb(120, 80, 30), 2f)) g.DrawLine(pen, cx + r * 0.7f, cy + r * 0.7f, cx + r * 1.9f, cy + r * 1.9f);
        using (var br = B(Color.FromArgb(200, 230, 255))) g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);
        using (var pen = P(Blue, 1.4f)) g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
    }

    private static void Arrow(Graphics g, Color c, float x1, float y, float x2, float head = 2.6f)
    {
        float dir = x2 >= x1 ? 1 : -1;
        using var pen = P(c, 1.6f); g.DrawLine(pen, x1, y, x2, y);
        using var br = B(c);
        g.FillPolygon(br, new[] { new PointF(x2 + dir * 1.2f, y), new PointF(x2 - dir * head, y - head), new PointF(x2 - dir * head, y + head) });
    }

    private static void Letter(Graphics g, string s, FontStyle st, Color c, float px = 13f, float dx = 0, float dy = 0)
    {
        using var f = new Font("Arial", px, st, GraphicsUnit.Pixel);
        using var br = B(c);
        var sz = g.MeasureString(s, f, 64, StringFormat.GenericTypographic);
        g.DrawString(s, f, br, 8 - sz.Width / 2 + dx, 7.5f - sz.Height / 2 + dy, StringFormat.GenericTypographic);
    }

    private static void Lines(Graphics g, float x, float y, float[] widths, float gap = 2.6f, float thick = 1.4f, Color? c = null, float[]? xs = null)
    {
        using var pen = P(c ?? Color.FromArgb(60, 60, 70), thick);
        for (int i = 0; i < widths.Length; i++)
        {
            float lx = xs != null ? xs[i] : x;
            g.DrawLine(pen, lx, y + i * gap, lx + widths[i], y + i * gap);
        }
    }

    private static void Draw(Graphics g, Ico k, Color? accent)
    {
        switch (k)
        {
            case Ico.New:
                Page(g, 3, 1, 10, 14);
                using (var br = B(Color.FromArgb(70, 170, 70))) g.FillEllipse(br, 0.5f, 0.5f, 5, 5);
                break;
            case Ico.Page:
                Page(g, 3, 1, 10, 14, accent);
                break;
            case Ico.Open:
            case Ico.FolderOpen:
            {
                using (var br = B(YellowDk)) g.FillRectangle(br, 1, 3, 5, 3);
                using (var br = B(Yellow)) g.FillRectangle(br, 1, 4, 11, 9);
                var flap = new[] { new PointF(3, 7), new PointF(15, 7), new PointF(12.5f, 13), new PointF(0.5f, 13) };
                using (var br = B(YellowLt)) g.FillPolygon(br, flap);
                using (var pen = P(YellowDk)) { g.DrawPolygon(pen, flap); g.DrawLines(pen, new[] { new PointF(1, 13), new PointF(1, 3.5f), new PointF(6, 3.5f), new PointF(7, 5), new PointF(12, 5), new PointF(12, 7) }); }
                break;
            }
            case Ico.Folder:
            {
                using (var br = B(YellowDk)) g.FillRectangle(br, 1, 3, 6, 3);
                using (var br = B(Yellow)) g.FillRectangle(br, 1, 4.5f, 14, 9);
                using (var pen = P(YellowDk)) g.DrawRectangle(pen, 1, 4.5f, 14, 9);
                break;
            }
            case Ico.Save: Floppy(g, 0, 0, 1f); break;
            case Ico.SaveAll: Floppy(g, 4.5f, 4.5f, 0.7f); Floppy(g, 0, 0, 0.7f); break;
            case Ico.Print:
                using (var br = B(Color.White)) g.FillRectangle(br, 4, 1, 8, 6);
                using (var pen = P(Outline)) g.DrawRectangle(pen, 4, 1, 8, 6);
                using (var br = B(Color.FromArgb(150, 150, 160))) g.FillRectangle(br, 1, 6, 14, 6);
                using (var pen = P(Outline)) g.DrawRectangle(pen, 1, 6, 14, 6);
                using (var br = B(Color.White)) g.FillRectangle(br, 4, 10, 8, 5);
                using (var pen = P(Outline)) g.DrawRectangle(pen, 4, 10, 8, 5);
                using (var br = B(Color.FromArgb(60, 200, 60))) g.FillRectangle(br, 12, 7.5f, 1.5f, 1.5f);
                break;
            case Ico.Cut:
                using (var pen = P(Color.FromArgb(90, 90, 100), 1.4f)) { g.DrawLine(pen, 5, 1, 11, 10); g.DrawLine(pen, 11, 1, 5, 10); }
                using (var pen = P(Color.FromArgb(200, 40, 40), 1.6f)) { g.DrawEllipse(pen, 2.5f, 10, 4, 4); g.DrawEllipse(pen, 9.5f, 10, 4, 4); }
                break;
            case Ico.Copy:
                Page(g, 1.5f, 1, 8, 10);
                Page(g, 6.5f, 5, 8, 10);
                break;
            case Ico.Paste:
                using (var br = B(Color.FromArgb(170, 120, 60))) g.FillRectangle(br, 1.5f, 2, 11, 13);
                using (var pen = P(Color.FromArgb(110, 70, 30))) g.DrawRectangle(pen, 1.5f, 2, 11, 13);
                using (var br = B(Color.FromArgb(190, 190, 200))) g.FillRectangle(br, 4, 0.5f, 6, 3.5f);
                Page(g, 6, 6, 8, 9.5f);
                break;
            case Ico.Undo:
            case Ico.Redo:
            {
                bool redo = k == Ico.Redo;
                if (redo) { g.TranslateTransform(16, 0); g.ScaleTransform(-1, 1); }
                using (var pen = P(Blue, 2f)) g.DrawArc(pen, 3.5f, 4.5f, 9, 8, 200, 190);
                using (var br = B(Blue)) g.FillPolygon(br, new[] { new PointF(1, 5.5f), new PointF(7.5f, 1.5f), new PointF(7.5f, 9f) });
                g.ResetTransform();
                break;
            }
            case Ico.Find: Magnifier(g, 6, 6, 4.2f); break;
            case Ico.Replace:
                Magnifier(g, 5.5f, 5.5f, 3.6f);
                Arrow(g, Color.FromArgb(40, 150, 60), 6, 12.5f, 14.5f);
                Arrow(g, Color.FromArgb(200, 70, 40), 14.5f, 15.2f, 8.5f, 2f);
                break;
            case Ico.GoTo:
                Lines(g, 2, 3, new[] { 12f, 12f, 12f, 12f }, 3f, 1.1f, Color.FromArgb(150, 150, 160));
                Arrow(g, Color.FromArgb(40, 110, 200), 1.5f, 8.2f, 12f, 3f);
                break;
            case Ico.WordWrap:
                Lines(g, 2, 3, new[] { 12f, 7f }, 3f, 1.4f);
                using (var pen = P(Blue, 1.4f)) g.DrawLines(pen, new[] { new PointF(2, 9.5f), new PointF(12, 9.5f), new PointF(13.5f, 11), new PointF(12, 12.5f), new PointF(6, 12.5f) });
                using (var br = B(Blue)) g.FillPolygon(br, new[] { new PointF(4.5f, 10.5f), new PointF(4.5f, 14.5f), new PointF(2, 12.5f) });
                break;
            case Ico.LineNumber:
                using (var br = B(Color.FromArgb(225, 225, 230))) g.FillRectangle(br, 1.5f, 2, 5, 12);
                using (var pen = P(Outline, 0.8f)) g.DrawRectangle(pen, 1.5f, 2, 12.5f, 12);
                Lines(g, 2.5f, 4.5f, new[] { 3f, 3f, 3f }, 3.2f, 1.2f, Blue);
                Lines(g, 8, 4.5f, new[] { 5f, 4f, 5f }, 3.2f, 1.1f, Color.FromArgb(120, 120, 130));
                break;
            case Ico.Ruler:
                using (var br = B(YellowLt)) g.FillRectangle(br, 1, 4.5f, 14, 7);
                using (var pen = P(YellowDk, 1f)) g.DrawRectangle(pen, 1, 4.5f, 14, 7);
                using (var pen = P(YellowDk, 0.9f)) for (int i = 0; i < 6; i++) g.DrawLine(pen, 3 + i * 2.2f, 4.5f, 3 + i * 2.2f, i % 2 == 0 ? 8.5f : 7f);
                break;
            case Ico.Bookmark:
                using (var br = B(Color.FromArgb(50, 110, 220))) g.FillPolygon(br, new[] { new PointF(3.5f, 1), new PointF(12.5f, 1), new PointF(12.5f, 15), new PointF(8, 11), new PointF(3.5f, 15) });
                using (var pen = P(Color.FromArgb(20, 60, 140))) g.DrawPolygon(pen, new[] { new PointF(3.5f, 1), new PointF(12.5f, 1), new PointF(12.5f, 15), new PointF(8, 11), new PointF(3.5f, 15) });
                break;
            case Ico.Bold: Letter(g, "B", FontStyle.Bold, Color.Black, 14); break;
            case Ico.Italic: Letter(g, "I", FontStyle.Italic | FontStyle.Bold, Color.Black, 14, 0.5f); break;
            case Ico.Underline:
                Letter(g, "U", FontStyle.Bold, Color.Black, 13, 0, -1);
                using (var pen = P(Color.Black, 1.3f)) g.DrawLine(pen, 3, 14, 13, 14);
                break;
            case Ico.Strike:
                Letter(g, "S", FontStyle.Bold, Color.Black, 14);
                using (var pen = P(Color.FromArgb(200, 30, 30), 1.4f)) g.DrawLine(pen, 2, 8, 14, 8);
                break;
            case Ico.TextColor:
                Letter(g, "A", FontStyle.Bold, Color.Black, 13, 0, -1.5f);
                using (var br = B(accent ?? Color.Red)) g.FillRectangle(br, 2, 13, 12, 2.5f);
                break;
            case Ico.BackColor:
                using (var br = B(accent ?? Color.Yellow)) g.FillRectangle(br, 1.5f, 1.5f, 13, 10);
                using (var pen = P(Color.FromArgb(150, 150, 150), 0.8f)) g.DrawRectangle(pen, 1.5f, 1.5f, 13, 10);
                Letter(g, "ab", FontStyle.Bold, Color.Black, 10, 0, -2.5f);
                using (var pen = P(Color.FromArgb(120, 120, 130), 1.6f)) g.DrawLine(pen, 3, 14, 13, 14);
                break;
            case Ico.AlignLeft: Lines(g, 2, 3, new[] { 12f, 7f, 12f, 7f, 12f }, 2.6f); break;
            case Ico.AlignCenter: Lines(g, 0, 3, new[] { 12f, 7f, 12f, 7f, 12f }, 2.6f, 1.4f, null, new[] { 2f, 4.5f, 2f, 4.5f, 2f }); break;
            case Ico.AlignRight: Lines(g, 0, 3, new[] { 12f, 7f, 12f, 7f, 12f }, 2.6f, 1.4f, null, new[] { 2f, 7f, 2f, 7f, 2f }); break;
            case Ico.Bullet:
                using (var br = B(Blue)) for (int i = 0; i < 3; i++) g.FillEllipse(br, 1.5f, 2.5f + i * 4.5f, 2.6f, 2.6f);
                Lines(g, 6, 3.8f, new[] { 8f, 8f, 8f }, 4.5f);
                break;
            case Ico.Indent:
            case Ico.Outdent:
            {
                Lines(g, 2, 2.5f, new[] { 12f }, 1, 1.4f);
                Lines(g, 7, 6f, new[] { 7f, 7f }, 3f);
                Lines(g, 2, 12f, new[] { 12f }, 1, 1.4f);
                var tri = k == Ico.Indent
                    ? new[] { new PointF(2, 5.5f), new PointF(2, 11f), new PointF(5.8f, 8.2f) }
                    : new[] { new PointF(5.8f, 5.5f), new PointF(5.8f, 11f), new PointF(2, 8.2f) };
                using (var br = B(Blue)) g.FillPolygon(br, tri);
                break;
            }
            case Ico.Image:
            case Ico.ClipImage:
                using (var br = B(Color.FromArgb(170, 215, 250))) g.FillRectangle(br, 1.5f, 2.5f, 13, 11);
                using (var br = B(Color.FromArgb(70, 160, 70))) g.FillPolygon(br, new[] { new PointF(1.5f, 13.5f), new PointF(6, 7), new PointF(9, 11), new PointF(11, 9), new PointF(14.5f, 13.5f) });
                using (var br = B(Color.FromArgb(250, 200, 50))) g.FillEllipse(br, 9.5f, 4, 3, 3);
                using (var pen = P(Outline)) g.DrawRectangle(pen, 1.5f, 2.5f, 13, 11);
                if (k == Ico.ClipImage) using (var br = B(Color.FromArgb(170, 120, 60))) g.FillRectangle(br, 5, 0.5f, 6, 3);
                break;
            case Ico.Link:
                using (var pen = P(Blue, 1.8f)) { g.DrawEllipse(pen, 1.5f, 6, 7, 4.5f); g.DrawEllipse(pen, 7.5f, 6, 7, 4.5f); }
                break;
            case Ico.HRule:
                using (var pen = P(Color.FromArgb(60, 60, 70), 1.8f)) g.DrawLine(pen, 1.5f, 8, 14.5f, 8);
                Lines(g, 3, 3.5f, new[] { 10f }, 1, 1f, Color.FromArgb(160, 160, 170));
                Lines(g, 3, 12.5f, new[] { 10f }, 1, 1f, Color.FromArgb(160, 160, 170));
                break;
            case Ico.Table:
                using (var br = B(Color.FromArgb(215, 230, 250))) g.FillRectangle(br, 1.5f, 2.5f, 13, 3);
                using (var pen = P(Blue, 1f))
                {
                    g.DrawRectangle(pen, 1.5f, 2.5f, 13, 11);
                    g.DrawLine(pen, 1.5f, 5.5f, 14.5f, 5.5f); g.DrawLine(pen, 1.5f, 9, 14.5f, 9);
                    g.DrawLine(pen, 6, 2.5f, 6, 13.5f); g.DrawLine(pen, 10.5f, 2.5f, 10.5f, 13.5f);
                }
                break;
        }
    }
}
