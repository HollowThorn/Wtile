namespace Wtile.Layouts;

/// <summary>
/// dwm's deck: like master-stack, the first <c>nmaster</c> windows tile side by side as columns
/// in a master area (width = <c>mfact</c> * work-area width, or full width with no stack). Unlike
/// master-stack, every remaining "stack" window gets the FULL stack-area rectangle rather than
/// being split into rows -- monocle-style overlap confined to the stack region, so only the
/// topmost stack window is visible at a time (relies on WindowManager z-order/focus the same way
/// <see cref="MonocleLayout"/> does, just scoped to the stack area instead of the whole screen).
/// No master-on-right variant -- dwm's deck doesn't have one either.
/// </summary>
public sealed class DeckLayout : ILayout
{
    public const string LayoutName = "deck";

    public string Name => LayoutName;
    public string Symbol => "[D]";

    public IReadOnlyList<LayoutRect> Arrange(in LayoutContext context)
    {
        int n = context.TiledWindowCount;
        var result = new LayoutRect[n];
        if (n == 0)
            return result;

        int nmaster = Math.Clamp((int)GetParam(context.Params, "nmaster", 1), 0, n);
        double mfact = Math.Clamp(GetParam(context.Params, "mfact", 0.55), 0.05, 0.95);
        int gap = Math.Max(0, (int)GetParam(context.Params, "gap", 0));

        var work = context.WorkArea;
        int masterCount = Math.Min(n, nmaster);
        bool hasStack = n > masterCount;
        int masterWidth = masterCount > 0
            ? (hasStack ? (int)Math.Round(work.Width * mfact) : work.Width)
            : 0;
        int stackWidth = work.Width - masterWidth;
        int stackX = work.X + masterWidth;

        // Master windows: side-by-side columns, each full height (identical to master-stack).
        int colX = work.X;
        for (int i = 0; i < masterCount; i++)
        {
            int remaining = masterCount - i;
            int width = (masterWidth - (colX - work.X)) / remaining;
            result[i] = Inset(new LayoutRect(colX, work.Y, width, work.Height), gap);
            colX += width;
        }

        // Stack windows: every one gets the SAME full stack rect (deck-style overlap), instead of
        // being divided into rows like master-stack.
        if (hasStack)
        {
            var stackRect = Inset(new LayoutRect(stackX, work.Y, stackWidth, work.Height), gap);
            for (int i = masterCount; i < n; i++)
                result[i] = stackRect;
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
