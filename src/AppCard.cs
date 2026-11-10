using System.Drawing.Drawing2D;

namespace PApps;

public sealed class AppCard : Control
{
    readonly AppEntry app;
    readonly string title;
    readonly Image? icon;
    readonly string mode;
    readonly Button star;
    readonly int itemPadding;
    readonly int textLine;
    const int StarSize = 22;
    bool hover;
    public AppCard(AppEntry app, string title, Image? icon, string mode, Font font, Action launch, Action favourite, ToolTip tips, int padding = 5, int gap = 5)
    {
        this.app = app; this.title = title; this.icon = icon; this.mode = mode;
        Font = font;
        itemPadding = Math.Clamp(padding, 0, 24);
        textLine = (int)Math.Ceiling(font.GetHeight()) + 2;
        var p = itemPadding;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        Size = mode switch
        {
            "Large Icons" => new Size(110 + 2 * p, 64 + 3 * p + 2 * textLine),
            "Medium Icons" => new Size(94 + 2 * p, 40 + 3 * p + 2 * textLine),
            "Small Icons" => new Size(Math.Clamp(TextRenderer.MeasureText(title, font).Width + 20 + StarSize + 4 * p, 110 + 2 * p, 180 + 2 * p), Math.Max(StarSize, textLine) + 2 * p),
            "List" => new Size(240, Math.Max(StarSize, textLine) + 2 * p),
            "Detailed Tiles" => new Size(600, Math.Max(28, 2 * textLine + 2) + 2 * p),
            "Small Tiles" => new Size(158 + 2 * p, Math.Max(28, 2 * textLine + 2) + 2 * p),
            "Large Tiles" => new Size(180 + 2 * p, 48 + 3 * p + 2 * textLine + 2),
            _ => new Size(180 + 2 * p, Math.Max(36, 2 * textLine + 2) + 2 * p)
        };
        Margin = new Padding(0, 0, Math.Clamp(gap, 0, 24), Math.Clamp(gap, 0, 24));
        AccessibleName = title;
        AccessibleDescription = app.Description;
        AccessibleRole = AccessibleRole.PushButton;
        star = new Button { Text = app.Favourite ? "★" : "☆", Size = new Size(StarSize, StarSize), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Symbol", 10), BackColor = Color.White, ForeColor = Color.FromArgb(21, 115, 104), TabStop = true, AccessibleName = app.Favourite ? "Remove " + title + " from favourites" : "Add " + title + " to favourites" };
        star.FlatAppearance.BorderSize = 0;
        star.Click += (_, _) => favourite();
        Controls.Add(star);
        Resize += (_, _) => star.Location = new Point(Width - StarSize - itemPadding, itemPadding);
        star.Location = new Point(Width - StarSize - itemPadding, itemPadding);
        tips.SetToolTip(this, string.IsNullOrWhiteSpace(app.Description) ? title : title + "\n" + app.Description);
        tips.SetToolTip(star, star.AccessibleName);
        Click += (_, _) => launch();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space) { e.Handled = e.SuppressKeyPress = true; launch(); }
            if (e.Control && e.KeyCode == Keys.D) { e.Handled = e.SuppressKeyPress = true; favourite(); }
        };
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
        GotFocus += (_, _) => Invalidate();
        LostFocus += (_, _) => Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = new Pen(hover || Focused ? Color.FromArgb(38, 143, 127) : Color.FromArgb(220, 228, 235), hover || Focused ? 2 : 1);
        using var fill = new SolidBrush(hover ? Color.FromArgb(241, 249, 247) : Color.FromArgb(249, 251, 253));
        using var shape = new GraphicsPath();
        var r = new Rectangle(1, 1, Width - 3, Height - 3);
        const int d = 16;
        shape.AddArc(r.Left, r.Top, d, d, 180, 90); shape.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        shape.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); shape.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); shape.CloseFigure();
        if (!mode.EndsWith("Icons", StringComparison.Ordinal) && mode != "List" || hover || Focused)
        { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(outline, shape); }
        using var textBrush = new SolidBrush(Color.FromArgb(27, 45, 63));
        if (mode.EndsWith("Icons", StringComparison.Ordinal) || mode == "List")
        {
            var stacked = mode is "Large Icons" or "Medium Icons";
            var size = mode == "Large Icons" ? 64 : mode == "Medium Icons" ? 40 : 20;
            var iconX = stacked ? (Width - size) / 2 : itemPadding;
            var iconTop = stacked ? itemPadding : (Height - size) / 2;
            if (icon is not null) e.Graphics.DrawImage(icon, new Rectangle(iconX, iconTop, size, size));
            using var textFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, Alignment = stacked ? StringAlignment.Center : StringAlignment.Near, LineAlignment = stacked ? StringAlignment.Near : StringAlignment.Center };
            if (!stacked) textFormat.FormatFlags = StringFormatFlags.NoWrap;
            var textRect = stacked ? new RectangleF(itemPadding, iconTop + size + itemPadding, Width - 2 * itemPadding, 2 * textLine)
                : new RectangleF(itemPadding + size + itemPadding, itemPadding, Math.Max(8, Width - size - StarSize - 4 * itemPadding), Height - 2 * itemPadding);
            e.Graphics.DrawString(title, Font, textBrush, textRect, textFormat);
            return;
        }
        var large = mode == "Large Tiles";
        var iconSize = large ? 48 : mode == "Small Tiles" || mode == "Detailed Tiles" ? 28 : 36;
        var iconY = large ? itemPadding : (Height - iconSize) / 2;
        if (icon is not null) e.Graphics.DrawImage(icon, new Rectangle(itemPadding, iconY, iconSize, iconSize));
        var x = large ? itemPadding : iconSize + 2 * itemPadding;
        var y = large ? itemPadding + iconSize + itemPadding : (Height - 2 * textLine - 2) / 2;
        using var secondary = new SolidBrush(Color.FromArgb(99, 115, 130));
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        var textWidth = Math.Max(10, Width - x - itemPadding - (large ? 0 : StarSize + itemPadding));
        e.Graphics.DrawString(title, Font, textBrush, new RectangleF(x, y, textWidth, textLine), format);
        var details = mode == "Detailed Tiles" ? app.Description : app.Category;
        using var smallFont = new Font(Font.FontFamily, Math.Max(8, Font.Size - 1), Font.Style);
        e.Graphics.DrawString(details, smallFont, secondary, new RectangleF(x, y + textLine + 2, Math.Max(10, Width - x - itemPadding), textLine), format);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) star.Font.Dispose();
        base.Dispose(disposing);
    }
}
