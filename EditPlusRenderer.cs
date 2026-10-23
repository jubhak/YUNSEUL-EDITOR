using System.Drawing.Drawing2D;

namespace YunseulMemo;

/// <summary>
/// EditPlus(클래식 Windows) 스타일 렌더러: 평평한 회색 도구 모음, 마우스를 올리면 1px 입체 테두리,
/// 이중선 그립, 식각(etched) 구분선, 상태 표시줄 패널의 sunken 테두리.
/// </summary>
internal sealed class EditPlusRenderer : ToolStripProfessionalRenderer
{
    public EditPlusRenderer() : base(new Colors()) { RoundedEdges = false; }

    private sealed class Colors : ProfessionalColorTable
    {
        public Colors() { UseSystemColors = false; }
        public override Color MenuItemSelected => Color.FromArgb(210, 225, 245);
        public override Color MenuItemBorder => Color.FromArgb(100, 140, 200);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(210, 225, 245);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(210, 225, 245);
        public override Color MenuBorder => Color.FromArgb(130, 130, 130);
        public override Color ToolStripDropDownBackground => Color.FromArgb(250, 250, 250);
        public override Color ImageMarginGradientBegin => Color.FromArgb(240, 240, 240);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(240, 240, 240);
        public override Color ImageMarginGradientEnd => Color.FromArgb(240, 240, 240);
        public override Color SeparatorDark => Color.FromArgb(160, 160, 160);
        public override Color SeparatorLight => Color.White;
        public override Color MenuStripGradientBegin => SystemColors.Control;
        public override Color MenuStripGradientEnd => SystemColors.Control;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown) { base.OnRenderToolStripBackground(e); return; }
        using var br = new SolidBrush(SystemColors.Control);
        e.Graphics.FillRectangle(br, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown) { base.OnRenderToolStripBorder(e); return; }
        if (e.ToolStrip is StatusStrip || e.ToolStrip is MenuStrip) return;
        // 도구 모음 아래에 클래식 식각선
        var r = e.AffectedBounds;
        using var dark = new Pen(Color.FromArgb(200, 200, 200));
        e.Graphics.DrawLine(dark, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var r = new Rectangle(0, 0, item.Width - 1, item.Height - 1);
        bool pressed = item.Pressed;
        bool hot = item.Selected;
        bool on = item is ToolStripButton tb && tb.Checked;
        if (!item.Enabled) return;
        if (!(pressed || hot || on)) return;

        var g = e.Graphics;
        if (pressed || on)
        {
            using var br = new SolidBrush(on && !pressed ? Color.FromArgb(225, 232, 245) : Color.FromArgb(205, 215, 235));
            g.FillRectangle(br, r);
            using var dark = new Pen(Color.FromArgb(110, 110, 120));
            using var light = new Pen(Color.White);
            g.DrawLine(dark, r.Left, r.Top, r.Right, r.Top); g.DrawLine(dark, r.Left, r.Top, r.Left, r.Bottom);
            g.DrawLine(light, r.Left, r.Bottom, r.Right, r.Bottom); g.DrawLine(light, r.Right, r.Top, r.Right, r.Bottom);
        }
        else
        {
            using var light = new Pen(Color.White);
            using var dark = new Pen(Color.FromArgb(110, 110, 120));
            g.DrawLine(light, r.Left, r.Top, r.Right - 1, r.Top); g.DrawLine(light, r.Left, r.Top, r.Left, r.Bottom - 1);
            g.DrawLine(dark, r.Left, r.Bottom, r.Right, r.Bottom); g.DrawLine(dark, r.Right, r.Top, r.Right, r.Bottom);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        var r = e.Item.ContentRectangle;
        using var dark = new Pen(Color.FromArgb(160, 160, 160));
        using var light = new Pen(Color.White);
        if (e.Vertical)
        {
            int x = e.Item.Width / 2 - 1;
            g.DrawLine(dark, x, 3, x, e.Item.Height - 4);
            g.DrawLine(light, x + 1, 3, x + 1, e.Item.Height - 4);
        }
        else
        {
            int y = e.Item.Height / 2;
            g.DrawLine(dark, r.Left + 28, y, r.Right, y);
            g.DrawLine(light, r.Left + 28, y + 1, r.Right, y + 1);
        }
    }

    protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
    {
        var g = e.Graphics;
        var r = e.GripBounds;
        using var dark = new Pen(Color.FromArgb(140, 140, 140));
        using var light = new Pen(Color.White);
        int x = r.Left + 2;
        for (int i = 0; i < 2; i++)
        {
            g.DrawLine(light, x + i * 3, r.Top + 3, x + i * 3, r.Bottom - 4);
            g.DrawLine(dark, x + i * 3 + 1, r.Top + 3, x + i * 3 + 1, r.Bottom - 4);
        }
    }

    protected override void OnRenderToolStripStatusLabelBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripStatusLabel lbl || lbl.BorderSides == ToolStripStatusLabelBorderSides.None) return;
        var r = new Rectangle(0, 1, lbl.Width - 1, lbl.Height - 3);
        using var dark = new Pen(Color.FromArgb(150, 150, 150));
        using var light = new Pen(Color.White);
        e.Graphics.DrawLine(dark, r.Left, r.Top, r.Right, r.Top); e.Graphics.DrawLine(dark, r.Left, r.Top, r.Left, r.Bottom);
        e.Graphics.DrawLine(light, r.Left, r.Bottom, r.Right, r.Bottom); e.Graphics.DrawLine(light, r.Right, r.Top, r.Right, r.Bottom);
    }
}
