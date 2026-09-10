using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

internal sealed class NetworkSegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "↓{0} ↑{1}";

    // Widest rate string ByteRateFormatter is realistically expected to produce for reservation
    // purposes -- "999.9M" covers throughput up to ~1GB/s (≈8Gbps), well beyond typical home/
    // office links. Sustained rates above that (rare) would still jitter the segment width; not
    // worth reserving for since it'd waste bar space on every normal reading.
    private const string WidestRate = "999.9M";

    private static string ComposeText(string? format, string down, string up) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, down, up);

    // See CpuSegment: reserves width for the widest expected rate rather than the live one, so
    // the segment doesn't shift the rest of the bar as the rate's digit/unit width changes.
    public float Measure(Graphics g, Font font) =>
        g.MeasureString(ComposeText(format, WidestRate, WidestRate), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        SystemStats.NetworkRate rate = SystemStats.GetNetworkRate();
        string text = ComposeText(format, ByteRateFormatter.Format(rate.DownBytesPerSec), ByteRateFormatter.Format(rate.UpBytesPerSec));

        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, bounds, fmt);
    }
}
