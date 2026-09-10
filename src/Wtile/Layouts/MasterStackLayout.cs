namespace Wtile.Layouts;

/// <summary>
/// Master-stack tiling: the first <c>nmaster</c> windows occupy a resizable master area (width =
/// <c>mfact</c> * work-area width), and any remaining windows occupy a stack area, stacked
/// vertically. Within the master area, windows sit side by side as columns (bug.n-style) rather
/// than stacked in rows like dwm -- e.g. 3 windows with nmaster=2 renders as three vertical
/// strips: one stack column plus two master columns. With a single window, or when
/// <c>nmaster</c> covers every window, there's no separate stack area and every window becomes a
/// master column sharing the full width. <paramref name="masterOnRight"/> mirrors which side the
/// master area sits on (dwm's <c>rmaster</c>): master-left ("[]=", the default, registered as
/// "master-stack") or master-right ("=[]", registered as "master-stack-right") -- same math,
/// just swapped columns.
/// </summary>
public sealed class MasterStackLayout(bool masterOnRight = false) : ILayout
{
    public const string LayoutName = "master-stack";
    public const string RightLayoutName = "master-stack-right";

    public string Name => masterOnRight ? RightLayoutName : LayoutName;
    public string Symbol => masterOnRight ? "=[]" : "[]=";

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

        int masterX = masterOnRight ? work.X + stackWidth : work.X;
        int stackX = masterOnRight ? work.X : work.X + masterWidth;

        // Master windows: side-by-side columns, each full height.
        int colX = masterX;
        for (int i = 0; i < masterCount; i++)
        {
            int remaining = masterCount - i;
            int width = (masterWidth - (colX - masterX)) / remaining;
            result[i] = Inset(new LayoutRect(colX, work.Y, width, work.Height), gap);
            colX += width;
        }

        // Stack windows: stacked rows, each full stack width (unchanged from dwm).
        int stackY = work.Y;
        for (int i = masterCount; i < n; i++)
        {
            int remaining = n - i;
            int height = (work.Height - (stackY - work.Y)) / remaining;
            result[i] = Inset(new LayoutRect(stackX, stackY, stackWidth, height), gap);
            stackY += height;
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
