using System.Drawing;
using Wtile.Core;

namespace Wtile.Bar.Segments;

internal sealed class LayoutSymbolSegment(WindowManager manager, BarTheme theme, int monitorIndex) : ISegment
{
    private const float Padding = 12f;

    public float Measure(Graphics g, Font font) => g.MeasureString(manager.GetLayoutSymbol(monitorIndex), font).Width + Padding;

    public void Draw(Graphics g, Font font, RectangleF bounds)
    {
        using var brush = new SolidBrush(theme.Foreground);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(manager.GetLayoutSymbol(monitorIndex), font, brush, bounds, format);
    }
}
