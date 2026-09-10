using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

/// <summary>Renders nothing (zero width, no draw) when the machine has no system battery -- a
/// desktop shouldn't show a garbage "BAT 0%".</summary>
internal sealed class BatterySegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "BAT {0}% {1}";

    private static string StateWord(SystemStats.BatteryStatus battery) =>
        battery.IsCharging ? "Charging" : battery.IsOnAc ? "AC" : "";

    private static string ComposeText(string? format, SystemStats.BatteryStatus battery) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, battery.Percent, StateWord(battery)).TrimEnd();

    public float Measure(Graphics g, Font font)
    {
        SystemStats.BatteryStatus battery = SystemStats.GetBattery();
        return battery.HasBattery ? g.MeasureString(ComposeText(format, battery), font).Width + Padding : 0;
    }

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        SystemStats.BatteryStatus battery = SystemStats.GetBattery();
        if (!battery.HasBattery)
            return;

        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(ComposeText(format, battery), font, brush, bounds, fmt);
    }
}
