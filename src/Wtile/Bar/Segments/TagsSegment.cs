using System.Drawing;
using Wtile.Commands;
using Wtile.Core;
using Monitor = Wtile.Core.Monitor;

namespace Wtile.Bar.Segments;

/// <summary>Clickable tag pills for one monitor's bar, dwm-style: active tag highlighted, empty
/// tags dimmed. An occupied-but-not-active tag gets exactly one of two mutually exclusive
/// indicators, per <see cref="BarConfig.OccupiedTagIndicator"/>: the default small corner marker
/// (dwm's occupancy square) with normal text and no fill, or (opt-in) a filled background with no
/// marker. Occupancy/active-tag are read from this segment's own monitor -- each monitor's bar
/// shows that monitor's own state, independent of the others.</summary>
internal sealed class TagsSegment(WindowManager manager, CommandRegistry commands, BarTheme theme, int monitorIndex, bool useBackgroundIndicator) : ISegment
{
    private const float TagWidth = 28f;
    private const float MarkerSize = 4f;
    private const float MarkerInset = 3f;

    public float Measure(Graphics g, Font font) => TagWidth * manager.TagCount;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        Monitor monitor = manager.Monitors[monitorIndex];
        for (int i = 0; i < manager.TagCount; i++)
        {
            var tagBounds = new RectangleF(bounds.X + i * TagWidth, bounds.Y, TagWidth, bounds.Height);
            bool hasWindows = manager.HasWindowsOnTag(monitorIndex, i);
            // While viewing all tags (dwm's view(~0)), highlight every occupied tag instead of
            // just one -- otherwise the bar would misleadingly still show a single "active" tag.
            bool isActive = monitor.IsViewingAllTags ? hasWindows : i == monitor.ActiveTagIndex;

            if (isActive)
            {
                using var activeBrush = new SolidBrush(theme.ActiveTag);
                g.FillRectangle(activeBrush, tagBounds);
            }
            else if (hasWindows && useBackgroundIndicator)
            {
                using var occupiedBrush = new SolidBrush(theme.InactiveTagBackground);
                g.FillRectangle(occupiedBrush, tagBounds);
            }

            Color textColor = isActive ? theme.Background : hasWindows ? theme.Foreground : theme.EmptyTagForeground;
            using var textBrush = new SolidBrush(textColor);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString((i + 1).ToString(), font, textBrush, tagBounds, format);

            if (hasWindows && !useBackgroundIndicator)
                g.FillRectangle(textBrush, tagBounds.X + MarkerInset, tagBounds.Y + MarkerInset, MarkerSize, MarkerSize);
        }
    }

    public void OnClick(float xInSegment)
    {
        int index = (int)(xInSegment / TagWidth);
        if (index >= 0 && index < manager.TagCount)
        {
            manager.SelectMonitor(monitorIndex);
            commands.TryExecute("view-tag", [(index + 1).ToString()]);
        }
    }
}
