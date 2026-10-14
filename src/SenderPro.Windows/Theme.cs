using System.Drawing.Drawing2D;

namespace SenderPro.Windows;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(244, 247, 251);
    public static readonly Color Ink = Color.FromArgb(25, 39, 58);
    public static readonly Color Muted = Color.FromArgb(107, 123, 145);
    public static readonly Color Accent = Color.FromArgb(9, 137, 128);
    public static readonly Color Line = Color.FromArgb(224, 231, 240);
    public static readonly Color Sidebar = Color.FromArgb(20, 31, 49);

    public static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        path.AddArc(rectangle.Left, rectangle.Top, d, d, 180, 90);
        path.AddArc(rectangle.Right - d, rectangle.Top, d, d, 270, 90);
        path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class ModernButton : Button
{
    private bool hovered;
    public bool Selected { get; set; }
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Color.White; ForeColor = Theme.Ink;
        Padding = new Padding(14, 6, 14, 6); Height = 42;
        Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var color = Selected ? Theme.Accent : BackColor;
        if (!Enabled) color = Theme.Line;
        else if (hovered && !Selected) color = ControlPaint.Light(color, .15f);
        using var path = Theme.Rounded(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 8);
        using var fill = new SolidBrush(color); e.Graphics.FillPath(fill, path);
        if (BackColor == Color.White && !Selected)
        { using var pen = new Pen(Theme.Line); e.Graphics.DrawPath(pen, path); }
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(10, 0, Width - 20, Height),
            !Enabled ? Theme.Muted : Selected ? Color.White : ForeColor, flags);
        if (Focused) { using var pen = new Pen(Theme.Accent) { DashStyle = DashStyle.Dot }; e.Graphics.DrawRectangle(pen, 5, 5, Width - 11, Height - 11); }
    }
}

internal sealed class ResponsivePage : FlowLayoutPanel
{
    private bool fitting;
    public ResponsivePage()
    {
        Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FlowDirection = FlowDirection.TopDown; WrapContents = false;
        RightToLeft = RightToLeft.Yes; Margin = Padding.Empty;
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Fit(); }
    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); Fit(); }
    public void Fit()
    {
        if (fitting || ClientSize.Width < 40) return;
        fitting = true;
        try
        {
            var width = Math.Max(180, ClientSize.Width - Padding.Horizontal - 8);
            foreach (Control child in Controls)
            {
                var available = Math.Max(150, width - child.Margin.Horizontal);
                if (child is Label label) label.MaximumSize = new Size(available, 0);
                else if (child is not Button && child is not CheckBox)
                {
                    child.MaximumSize = new Size(available, 0);
                    child.Width = available;
                }
            }
        }
        finally { fitting = false; }
    }
}
