using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

/// <summary>Renders nothing (zero width, no draw) when the machine has no system battery -- a
/// desktop shouldn't show a garbage "BAT 0%".</summary>
internal sealed class BatterySegment(BarTheme theme, string? format) : ISegment
{
    private const float Padding = 12f;
    private const string DefaultFormat = "BAT {0}% {1}";
    private const int MaxPercent = 100;
    private const string WidestStateWord = "Charging"; // longest of "", "AC", "Charging"

    private static string StateWord(SystemStats.BatteryStatus battery) =>
        battery.IsCharging ? "Charging" : battery.IsOnAc ? "AC" : "";

    private static string ComposeText(string? format, int percent, string stateWord) =>
        SegmentFormat.Apply(format ?? DefaultFormat, DefaultFormat, percent, stateWord).TrimEnd();

    public float Measure(Graphics g, Font font)
    {
        // Reserves width for the widest percent/state combination rather than the live one, so
        // the segment doesn't shift the rest of the bar as the percent's digit count or the
        // charging state changes.
        if (!SystemStats.GetBattery().HasBattery)
            return 0;
        return g.MeasureString(ComposeText(format, MaxPercent, WidestStateWord), font).Width + Padding;
    }

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        SystemStats.BatteryStatus battery = SystemStats.GetBattery();
        if (!battery.HasBattery)
            return;

        using var brush = new SolidBrush(theme.Foreground);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(ComposeText(format, battery.Percent, StateWord(battery)), font, brush, bounds, fmt);
    }
}
