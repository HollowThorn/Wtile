using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

internal sealed class NetworkSegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "↓{0} ↑{1}";

    private static string ComposeText(string? format)
    {
        SystemStats.NetworkRate rate = SystemStats.GetNetworkRate();
        return SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat,
            ByteRateFormatter.Format(rate.DownBytesPerSec), ByteRateFormatter.Format(rate.UpBytesPerSec));
    }

    public float Measure(Graphics g, Font font) => g.MeasureString(ComposeText(format), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(ComposeText(format), font, brush, bounds, fmt);
    }
}
