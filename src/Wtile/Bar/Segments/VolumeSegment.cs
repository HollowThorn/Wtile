using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

/// <summary>Renders nothing when no audio device could be resolved, same convention as
/// BatterySegment on a machine with no battery.</summary>
internal sealed class VolumeSegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "VOL {0}%{1}";

    // The mute indicator's leading space lives in the glyph itself, not the format string, so an
    // unmuted "VOL 42%" doesn't carry a trailing space.
    private static bool TryComposeText(string? format, out string text)
    {
        if (!VolumeStats.TryGetVolume(out int percent, out bool muted))
        {
            text = "";
            return false;
        }
        text = SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, percent, muted ? " MUTE" : "");
        return true;
    }

    public float Measure(Graphics g, Font font) =>
        TryComposeText(format, out string text) ? g.MeasureString(text, font).Width + Padding : 0;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        if (!TryComposeText(format, out string text))
            return;

        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, bounds, fmt);
    }
}
