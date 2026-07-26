using AsusFanProfileSwitcher.Models;

namespace AsusFanProfileSwitcher.Controls;

internal sealed class CurveChart : Control
{
    private static readonly Color[] CurveColors =
    [
        Color.FromArgb(235, 43, 58),
        Color.FromArgb(0, 210, 190),
        Color.FromArgb(245, 174, 43),
        Color.FromArgb(92, 142, 255),
        Color.FromArgb(190, 91, 255)
    ];

    private IReadOnlyList<FanCurve> _curves = [];

    public CurveChart()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(260, 190);
        BackColor = Color.FromArgb(18, 21, 25);
    }

    public IReadOnlyList<FanCurve> Curves
    {
        get => _curves;
        set
        {
            _curves = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);
        var chart = new Rectangle(42, 18, Math.Max(10, Width - 61), Math.Max(10, Height - 54));

        using var gridPen = new Pen(Color.FromArgb(44, 48, 55), 1);
        using var labelFont = new Font("Segoe UI", 7.5F);
        using var labelBrush = new SolidBrush(Color.FromArgb(122, 130, 140));
        for (var step = 0; step <= 4; step++)
        {
            var x = chart.Left + chart.Width * step / 4;
            var y = chart.Bottom - chart.Height * step / 4;
            graphics.DrawLine(gridPen, x, chart.Top, x, chart.Bottom);
            graphics.DrawLine(gridPen, chart.Left, y, chart.Right, y);
            graphics.DrawString($"{step * 25}", labelFont, labelBrush, x - 7, chart.Bottom + 5);
            graphics.DrawString($"{step * 25}", labelFont, labelBrush, 7, y - 6);
        }
        graphics.DrawString("°C", labelFont, labelBrush, chart.Right - 5, chart.Bottom + 19);
        graphics.DrawString("%", labelFont, labelBrush, 17, chart.Top - 13);

        if (_curves.Count == 0)
        {
            using var emptyFont = new Font("Segoe UI", 9F);
            graphics.DrawString(
                "No curve points found in this profile",
                emptyFont,
                labelBrush,
                chart,
                new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
            return;
        }

        for (var curveIndex = 0; curveIndex < _curves.Count; curveIndex++)
        {
            var curve = _curves[curveIndex];
            if (curve.Points.Count == 0)
            {
                continue;
            }

            var color = CurveColors[curveIndex % CurveColors.Length];
            using var curvePen = new Pen(color, 2.2F);
            using var pointBrush = new SolidBrush(color);
            var points = curve.Points
                .Select(point => new PointF(
                    chart.Left + Math.Clamp(point.Temperature, 0, 100) / 100F * chart.Width,
                    chart.Bottom - Math.Clamp(point.Duty, 0, 100) / 100F * chart.Height))
                .ToArray();
            if (points.Length > 1)
            {
                graphics.DrawLines(curvePen, points);
            }
            foreach (var point in points)
            {
                graphics.FillEllipse(pointBrush, point.X - 3, point.Y - 3, 6, 6);
            }
        }
    }
}
