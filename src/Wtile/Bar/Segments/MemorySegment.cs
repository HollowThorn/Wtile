using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

internal sealed class MemorySegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "MEM {0}%";
    private const int MaxPercent = 100; // widest value this can render

    private static string ComposeText(string? format, int percent) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, percent);

    // See CpuSegment: reserves width for 100% rather than the live value, so the segment doesn't
    // shift the rest of the bar as the digit count changes.
    public float Measure(Graphics g, Font font) => g.MeasureString(ComposeText(format, MaxPercent), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(ComposeText(format, (int)SystemStats.GetMemoryPercent()), font, brush, bounds, fmt);
    }
}
