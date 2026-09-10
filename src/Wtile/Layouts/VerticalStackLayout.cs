namespace Wtile.Layouts;

/// <summary>
/// Every tiled window gets a full-width horizontal strip, stacked top to bottom, dividing the
/// height evenly -- no master/stack distinction, so nmaster/mfact are irrelevant here (only gap
/// applies). Registered as "vertical" (the windows stack vertically, one row each).
/// </summary>
public sealed class VerticalStackLayout : ILayout
{
    public const string LayoutName = "vertical";

    public string Name => LayoutName;
    public string Symbol => "=";

    public IReadOnlyList<LayoutRect> Arrange(in LayoutContext context)
    {
        int n = context.TiledWindowCount;
        var result = new LayoutRect[n];
        if (n == 0)
            return result;

        int gap = Math.Max(0, (int)GetParam(context.Params, "gap", 0));
        var work = context.WorkArea;

        int y = work.Y;
        for (int i = 0; i < n; i++)
        {
            int remaining = n - i;
            int height = (work.Height - (y - work.Y)) / remaining;
            result[i] = Inset(new LayoutRect(work.X, y, work.Width, height), gap);
            y += height;
        }

        return result;
    }

    private static LayoutRect Inset(LayoutRect rect, int gap)
    {
        if (gap <= 0)
            return rect;
        int half = gap / 2;
        int width = Math.Max(1, rect.Width - gap);
        int height = Math.Max(1, rect.Height - gap);
        return new LayoutRect(rect.X + half, rect.Y + half, width, height);
    }

    private static double GetParam(IReadOnlyDictionary<string, double> parameters, string key, double fallback)
        => parameters.TryGetValue(key, out double value) ? value : fallback;
}
