using AsusFanProfileSwitcher.Models;

namespace AsusFanProfileSwitcher.Controls;

internal sealed class ProfileCard : Control
{
    private readonly Color _surface = Color.FromArgb(24, 27, 32);
    private readonly Color _surfaceHover = Color.FromArgb(31, 35, 41);
    private readonly Color _accent = Color.FromArgb(231, 38, 53);
    private bool _hovered;

    public ProfileCard(FanProfile profile, bool active)
    {
        Profile = profile;
        IsActive = active;
        Size = new Size(194, 194);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        TabStop = true;
        AccessibleName = $"Apply {profile.DisplayName} fan profile";
        AccessibleRole = AccessibleRole.PushButton;
    }

    public FanProfile Profile { get; }
    public bool IsActive { get; }
    public event EventHandler? Invoked;
    public event EventHandler? EditInvoked;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (new Rectangle(Width - 37, 8, 29, 29).Contains(e.Location))
            {
                EditInvoked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Invoked?.Invoke(this, EventArgs.Empty);
            }
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            Invoked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? Color.Black);

        using var background = new SolidBrush(_hovered ? _surfaceHover : _surface);
        graphics.FillRectangle(background, 0, 0, Width - 1, Height - 1);

        using var border = new Pen(IsActive ? _accent : Color.FromArgb(49, 53, 60), IsActive ? 2 : 1);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        if (IsActive)
        {
            using var activeStrip = new SolidBrush(_accent);
            graphics.FillRectangle(activeStrip, 0, 0, 4, Height);
        }

        DrawIcon(graphics, new Rectangle(18, 21, 68, 68));

        using var editPen = new Pen(Color.FromArgb(150, 158, 169), 1.7F);
        graphics.DrawLine(editPen, Width - 29, 15, Width - 17, 27);
        graphics.DrawLine(editPen, Width - 31, 27, Width - 28, 28);
        graphics.DrawLine(editPen, Width - 31, 27, Width - 30, 24);

        using var titleFont = new Font("Segoe UI Semibold", 11F);
        using var smallFont = new Font("Segoe UI", 8.5F);
        using var titleBrush = new SolidBrush(Color.FromArgb(239, 241, 244));
        using var mutedBrush = new SolidBrush(Color.FromArgb(135, 143, 154));
        using var activeBrush = new SolidBrush(_accent);
        var titleRect = new RectangleF(17, 116, Width - 32, 26);
        graphics.DrawString(
            Profile.DisplayName,
            titleFont,
            titleBrush,
            titleRect,
            new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            });
        if (!string.Equals(Profile.DisplayName, Profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            graphics.DrawString(
                Profile.Name,
                smallFont,
                mutedBrush,
                new RectangleF(17, 145, Width - 32, 19),
                new StringFormat
                {
                    Trimming = StringTrimming.EllipsisPath,
                    FormatFlags = StringFormatFlags.NoWrap
                });
        }
        graphics.DrawString(
            IsActive ? "ACTIVE" : "APPLY PROFILE",
            smallFont,
            IsActive ? activeBrush : mutedBrush,
            17,
            Height - 26);
    }

    private void DrawIcon(Graphics graphics, Rectangle bounds)
    {
        var name = Profile.Name.ToLowerInvariant();
        using var pen = new Pen(_accent, 2.4F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        if (name.Contains("turbo"))
        {
            var points = new[]
            {
                new Point(bounds.Left + 40, bounds.Top),
                new Point(bounds.Left + 13, bounds.Top + 39),
                new Point(bounds.Left + 34, bounds.Top + 39),
                new Point(bounds.Left + 24, bounds.Bottom),
                new Point(bounds.Right - 7, bounds.Top + 27),
                new Point(bounds.Left + 43, bounds.Top + 27)
            };
            graphics.DrawLines(pen, points);
            return;
        }

        if (name.Contains("full") || name.Contains("max"))
        {
            for (var offset = 0; offset < 3; offset++)
            {
                var x = bounds.Left + 7 + offset * 18;
                graphics.DrawLines(pen, new Point[]
                {
                    new Point(x, bounds.Top + 12),
                    new Point(x + 15, bounds.Top + 34),
                    new Point(x, bounds.Top + 57)
                });
            }
            return;
        }

        DrawFan(graphics, pen, bounds);
        if (name.Contains("silent"))
        {
            graphics.DrawArc(pen, bounds.Left + 7, bounds.Bottom - 13, 52, 15, 15, 150);
        }
        else if (!name.Contains("standard"))
        {
            graphics.DrawLine(pen, bounds.Left + 5, bounds.Bottom - 2, bounds.Left + 25, bounds.Bottom - 20);
            graphics.DrawLine(pen, bounds.Left + 25, bounds.Bottom - 20, bounds.Left + 43, bounds.Bottom - 10);
            graphics.DrawLine(pen, bounds.Left + 43, bounds.Bottom - 10, bounds.Right - 3, bounds.Top + 34);
        }
    }

    private static void DrawFan(Graphics graphics, Pen pen, Rectangle bounds)
    {
        var center = new PointF(bounds.Left + bounds.Width / 2F, bounds.Top + bounds.Height / 2F);
        graphics.DrawEllipse(pen, bounds.Left + 8, bounds.Top + 8, bounds.Width - 16, bounds.Height - 16);
        graphics.DrawEllipse(pen, center.X - 5, center.Y - 5, 10, 10);
        for (var angle = 0; angle < 360; angle += 90)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(center.X, center.Y);
            graphics.RotateTransform(angle);
            graphics.DrawBezier(pen, 5, -2, 11, -22, 26, -20, 20, -4);
            graphics.Restore(state);
        }
    }
}
