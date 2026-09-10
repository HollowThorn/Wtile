namespace Wtile.Layouts;

/// <summary>
/// dwm's monocle: every tiled window gets the full work area, stacked in z-order -- only the
/// focused one is visible. Relies on WindowManager preserving z-order (SWP_NOZORDER) and
/// focus-next/prev bringing the target window to the top when switching.
/// </summary>
public sealed class MonocleLayout : ILayout
{
    public const string LayoutName = "monocle";

    public string Name => LayoutName;
    public string Symbol => "[M]";

    public IReadOnlyList<LayoutRect> Arrange(in LayoutContext context)
    {
        var result = new LayoutRect[context.TiledWindowCount];
        Array.Fill(result, context.WorkArea);
        return result;
    }
}
