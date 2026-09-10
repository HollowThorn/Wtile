using System.Drawing;
using Wtile.Bar.Segments;

namespace Wtile.Bar;

internal readonly record struct SegmentHit(ISegment Segment, RectangleF Bounds);

/// <summary>
/// Pure layout+paint logic for one bar: measures fixed-width segments, gives whatever's left to
/// the one flexible segment (the title), draws left-to-right / right-to-left, and returns the
/// hit-test rectangles <see cref="BarWindow"/> uses to dispatch clicks.
/// </summary>
internal sealed class BarRenderer(IReadOnlyList<ISegment> left, IReadOnlyList<ISegment> right, BarTheme theme)
{
    public List<SegmentHit> Draw(Graphics g, Font font, RectangleF clientBounds)
    {
        var hits = new List<SegmentHit>(left.Count + right.Count);

        using (var background = new SolidBrush(theme.Background))
            g.FillRectangle(background, clientBounds);

        var leftWidths = new float[left.Count];
        float leftFixedTotal = 0;
        int flexIndex = -1;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].IsFlexible)
            {
                flexIndex = i;
                continue;
            }
            leftWidths[i] = left[i].Measure(g, font);
            leftFixedTotal += leftWidths[i];
        }

        var rightWidths = new float[right.Count];
        float rightTotal = 0;
        for (int i = 0; i < right.Count; i++)
        {
            rightWidths[i] = right[i].Measure(g, font);
            rightTotal += rightWidths[i];
        }

        if (flexIndex >= 0)
            leftWidths[flexIndex] = MathF.Max(0, clientBounds.Width - leftFixedTotal - rightTotal);

        float x = clientBounds.X;
        for (int i = 0; i < left.Count; i++)
        {
            var bounds = new RectangleF(x, clientBounds.Y, leftWidths[i], clientBounds.Height);
            left[i].Draw(g, font, bounds);
            hits.Add(new SegmentHit(left[i], bounds));
            x += leftWidths[i];
        }

        float rx = clientBounds.Right - rightTotal;
        for (int i = 0; i < right.Count; i++)
        {
            var bounds = new RectangleF(rx, clientBounds.Y, rightWidths[i], clientBounds.Height);
            right[i].Draw(g, font, bounds);
            hits.Add(new SegmentHit(right[i], bounds));
            rx += rightWidths[i];
        }

        return hits;
    }
}
