using AsusFanProfileSwitcher.Services;

namespace AsusFanProfileSwitcher.Controls;

internal sealed class FanReadingCard : Control
{
    private FanReading _reading;
    private bool _isSelected;

    public FanReadingCard(FanReading reading, string displayName)
    {
        _reading = reading;
        DisplayName = displayName;
        Height = 94;
        Width = 330;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        AccessibleName = $"{displayName}, {reading.Rpm:0} RPM";
    }

    public string DisplayName { get; private set; }
    public string SensorId => _reading.Id;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            Invalidate();
        }
    }
    public event EventHandler? Invoked;
    public event EventHandler? RenameInvoked;

    public void UpdateReading(FanReading reading, string displayName)
    {
        _reading = reading;
        DisplayName = displayName;
        AccessibleName = $"{displayName}, {reading.Rpm:0} RPM";
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (RenameBounds.Contains(e.Location))
            {
                RenameInvoked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Invoked?.Invoke(this, EventArgs.Empty);
            }
        }
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(_isSelected
            ? Color.FromArgb(35, 31, 36)
            : Color.FromArgb(26, 29, 34));

        using var accentBrush = new SolidBrush(Color.FromArgb(232, 39, 54));
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 238, 242));
        using var mutedBrush = new SolidBrush(Color.FromArgb(126, 135, 146));
        using var titleFont = new Font("Segoe UI Semibold", 9.5F);
        using var valueFont = new Font("Bahnschrift SemiBold", 15F);
        using var smallFont = new Font("Segoe UI", 8F);

        graphics.FillRectangle(accentBrush, 0, 0, _isSelected ? 5 : 3, Height);
        if (_isSelected)
        {
            using var borderPen = new Pen(Color.FromArgb(104, 41, 49));
            graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
        graphics.DrawString(DisplayName, titleFont, titleBrush, 16, 12);
        graphics.DrawString("RENAME", smallFont, mutedBrush, RenameBounds);
        graphics.DrawString($"{_reading.Rpm:0}", valueFont, titleBrush, 16, 41);
        graphics.DrawString("RPM", smallFont, mutedBrush, 78, 54);

        var percentageText = _reading.Percentage is { } percentage
            ? $"{percentage:0}%"
            : "N/A";
        graphics.DrawString(percentageText, valueFont, accentBrush, Width - 68, 41);

        var gauge = new Rectangle(116, 67, Math.Max(20, Width - 202), 5);
        using var gaugeBackground = new SolidBrush(Color.FromArgb(52, 56, 63));
        graphics.FillRectangle(gaugeBackground, gauge);
        if (_reading.Percentage is { } duty)
        {
            graphics.FillRectangle(
                accentBrush,
                gauge.X,
                gauge.Y,
                (int)(gauge.Width * Math.Clamp(duty, 0, 100) / 100F),
                gauge.Height);
        }
    }

    private Rectangle RenameBounds => new(Width - 74, 10, 58, 22);
}
