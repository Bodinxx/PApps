using System.Drawing.Drawing2D;

namespace PApps;

public sealed class CategoryHeader : Control
{
    public CategoryHeader(string title, Font font)
    {
        Text = title;
        Font = font;
        Height = Math.Max(30, (int)Math.Ceiling(font.GetHeight()) + 10);
        Width = 300;
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.StaticText;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        const int d = 14;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb(224, 239, 251));
        using var ink = new SolidBrush(Color.FromArgb(45, 85, 120));
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawString(Text, Font, ink, new RectangleF(10, 0, Width - 20, Height), format);
    }
}
