namespace Wtile.Layouts;

/// <summary>
/// dwm's fibonacci "dwindle" layout: window 0 takes the left column (width = <c>mfact</c> *
/// work-area width, full height), then each remaining rectangle is halved and handed to the next
/// window, alternating the cut axis -- vertical (left/right), then horizontal (top/bottom), then
/// vertical again, and so on -- so windows spiral inward. The very last window gets 100% of
/// whatever rectangle is left over rather than being split further. Unlike master-stack/deck,
/// dwindle has no master-count concept: <c>nmaster</c> is not read at all, only <c>mfact</c>
/// (applied to the very first split only, matching dwm's fibonacci.diff) and <c>gap</c>.
/// </summary>
public sealed class DwindleLayout : ILayout
{
    public const string LayoutName = "dwindle";

    public string Name => LayoutName;
    public string Symbol => "[\\]";

    public IReadOnlyList<LayoutRect> Arrange(in LayoutContext context)
    {
        int n = context.TiledWindowCount;
        var result = new LayoutRect[n];
        if (n == 0)
            return result;

        double mfact = Math.Clamp(GetParam(context.Params, "mfact", 0.55), 0.05, 0.95);
        int gap = Math.Max(0, (int)GetParam(context.Params, "gap", 0));
        var remaining = context.WorkArea;

        if (n == 1)
        {
            result[0] = Inset(remaining, gap);
            return result;
        }

        for (int i = 0; i < n - 1; i++)
        {
            double fraction = i == 0 ? mfact : 0.5;
            bool splitVertically = i % 2 == 0; // vertical cut (left/right) on even steps, horizontal (top/bottom) on odd

            if (splitVertically)
            {
                int width = (int)Math.Round(remaining.Width * fraction);
                result[i] = Inset(new LayoutRect(remaining.X, remaining.Y, width, remaining.Height), gap);
                remaining = new LayoutRect(remaining.X + width, remaining.Y, remaining.Width - width, remaining.Height);
            }
            else
            {
                int height = (int)Math.Round(remaining.Height * fraction);
                result[i] = Inset(new LayoutRect(remaining.X, remaining.Y, remaining.Width, height), gap);
                remaining = new LayoutRect(remaining.X, remaining.Y + height, remaining.Width, remaining.Height - height);
            }
        }

        result[n - 1] = Inset(remaining, gap); // last window takes 100% of whatever is left over

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
