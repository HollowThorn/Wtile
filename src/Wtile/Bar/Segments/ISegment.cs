using System.Drawing;

namespace Wtile.Bar.Segments;

/// <summary>
/// One drawable, optionally-clickable piece of the bar (tags, layout symbol, title, clock).
/// Future segments (CPU%, RAM%, ...) are just new implementations of this interface.
/// </summary>
public interface ISegment
{
    /// <summary>Width in pixels this segment needs right now, given the bar's font.</summary>
    float Measure(Graphics g, Font font);

    /// <summary>True for the one segment (the window title) that should absorb leftover bar width instead of a fixed measured width.</summary>
    bool IsFlexible => false;

    /// <summary>Draws into <paramref name="bounds"/> (already sized to this segment's Measure width).</summary>
    void Draw(Graphics g, Font font, RectangleF bounds);

    /// <summary>Called when the user clicks within this segment's bounds; <paramref name="xInSegment"/> is relative to the segment's left edge.</summary>
    void OnClick(float xInSegment) { }
}
