using System.Drawing;
using System.Globalization;

namespace Wtile.Bar.Segments;

internal sealed class ClockSegment(BarTheme theme) : ISegment
{
    private const float Padding = 12f;
    private const string Format = "ddd dd-MMM-yyyy HH:mm:ss"; // e.g. "Thu 10-Sep-2026 02:35:11"

    private static string Now() => DateTime.Now.ToString(Format, CultureInfo.InvariantCulture);

    public float Measure(Graphics g, Font font) => g.MeasureString(Now(), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        using var brush = new SolidBrush(theme.Foreground);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Now(), font, brush, bounds, format);
    }
}
