using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

/// <summary>Shows the focused window's title. Absorbs whatever space is left in the bar.</summary>
internal sealed class TitleSegment(WindowManager manager, BarTheme theme) : ISegment
{
    private const float Padding = 8f;

    public bool IsFlexible => true;

    public float Measure(Graphics g, Font font) => 0; // flexible: BarRenderer decides the width

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        if (bounds.Width <= 0)
            return;
        var textBounds = new RectangleF(bounds.X + Padding, bounds.Y, MathF.Max(0, bounds.Width - Padding), bounds.Height);
        using var brush = new SolidBrush(theme.Foreground);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        // bug.n-style: a "~" prefix marks the focused window as floating (excluded from tiling).
        string text = manager.IsFocusedFloating ? $"~ {manager.FocusedTitle}" : manager.FocusedTitle;
        g.DrawString(text, font, brush, textBounds, format);
    }
}
