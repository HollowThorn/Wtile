using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

/// <summary>Renders nothing when no audio device could be resolved, same convention as
/// BatterySegment on a machine with no battery.</summary>
internal sealed class VolumeSegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "VOL {0}%";
    private const string MutedText = "MUTED"; // shown bare, ignoring the format string's own prefix/suffix

    private static string ComposeText(string? format, int percent) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, percent);

    public float Measure(Graphics g, Font font)
    {
        if (!VolumeStats.TryGetVolume(out _, out _))
            return 0;
        // Reserves width for whichever of "VOL 100%"/"MUTED" is wider, so toggling mute or the
        // percent's digit count doesn't shift the rest of the bar.
        float unmutedWidth = g.MeasureString(ComposeText(format, 100), font).Width;
        float mutedWidth = g.MeasureString(MutedText, font).Width;
        return Math.Max(unmutedWidth, mutedWidth) + Padding;
    }

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        if (!VolumeStats.TryGetVolume(out int percent, out bool muted))
            return;

        string text = muted ? MutedText : ComposeText(format, percent);

        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, bounds, fmt);
    }
}
