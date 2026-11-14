using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace IMGViewer;

internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    internal TrayMenuRenderer() : base(new TrayColors()) { RoundedEdges = true; }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(Drawing.Color.FromArgb(32, 32, 32));
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(54, 54, 54));
        float scale = e.Graphics.DpiX / 96F;
        using var shape = RoundedRectangle(new Drawing.RectangleF(5 * scale, 2 * scale,
            Math.Max(1, e.Item.Width - 10 * scale), Math.Max(1, e.Item.Height - 4 * scale)), 4 * scale);
        e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, shape);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        var color = e.Item.Enabled ? Drawing.Color.FromArgb(238, 238, 238) : Drawing.Color.Gray;
        int inset = (int)Math.Round(16 * e.Graphics.DpiX / 96F);
        var bounds = new Drawing.Rectangle(inset, 0, Math.Max(1, e.Item.Width - inset * 2), e.Item.Height);
        var flags = Forms.TextFormatFlags.Left | Forms.TextFormatFlags.VerticalCenter |
            Forms.TextFormatFlags.SingleLine | Forms.TextFormatFlags.EndEllipsis | Forms.TextFormatFlags.NoPrefix;
        if (e.ToolStrip?.RightToLeft == Forms.RightToLeft.Yes)
            flags = (flags & ~Forms.TextFormatFlags.Left) | Forms.TextFormatFlags.Right | Forms.TextFormatFlags.RightToLeft;
        Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, color, flags);
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(66, 66, 66));
        e.Graphics.DrawLine(pen, 0, e.Item.Height / 2, e.Item.Width, e.Item.Height / 2);
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        float scale = e.Graphics.DpiX / 96F;
        using var shape = RoundedRectangle(new Drawing.RectangleF(0.5F, 0.5F,
            Math.Max(1, e.ToolStrip.Width - 1), Math.Max(1, e.ToolStrip.Height - 1)), 8 * scale);
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(48,48,48));
        e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, shape);
    }

    internal static Drawing.Drawing2D.GraphicsPath RoundedRectangle(Drawing.RectangleF bounds, float radius)
    {
        var path = new Drawing.Drawing2D.GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class TrayColors : Forms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(32, 32, 32);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(48, 48, 48);
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(54, 54, 54);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(54, 54, 54);
        public override Drawing.Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Drawing.Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Drawing.Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    }
}

internal sealed class TrayContextMenu : Forms.ContextMenuStrip
{
    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        float scale = DeviceDpi / 96F;
        MinimumSize = new Drawing.Size((int)(240 * scale), 0);
        Padding = new Forms.Padding(0, (int)(8 * scale), 0, (int)(8 * scale));
        foreach (Forms.ToolStripItem item in Items)
        {
            item.AutoSize = false;
            item.Size = new Drawing.Size((int)(240 * scale), (int)((item is Forms.ToolStripSeparator ? 17 : 34) * scale));
            item.Padding = new Forms.Padding((int)(20 * scale), 0, (int)(18 * scale), 0);
        }
        base.OnOpening(e);
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 2 || Height < 2) return;
        using var shape = TrayMenuRenderer.RoundedRectangle(new Drawing.RectangleF(0,0,Width,Height), 8 * DeviceDpi / 96F);
        var previous = Region;
        Region = new Drawing.Region(shape);
        previous?.Dispose();
    }
}
