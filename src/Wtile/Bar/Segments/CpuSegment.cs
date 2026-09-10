using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

internal sealed class CpuSegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "CPU {0}%";
    private const int MaxPercent = 100; // widest value this can render

    private static string ComposeText(string? format, int percent) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, percent);

    // Reserves width for the widest possible value (100%) instead of the live one, so the
    // segment -- and everything laid out after it -- doesn't shift as the digit count changes
    // between 1/2/3 digits.
    public float Measure(Graphics g, Font font) => g.MeasureString(ComposeText(format, MaxPercent), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(ComposeText(format, (int)Math.Round(SystemStats.GetCpuPercent())), font, brush, bounds, fmt);
    }
}
