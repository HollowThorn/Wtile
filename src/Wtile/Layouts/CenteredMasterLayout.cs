namespace Wtile.Layouts;

/// <summary>
/// dwm's centeredmaster: the master column sits horizontally centered (width = <c>mfact</c> *
/// work-area width), with stack windows alternating into columns flanking it on the right then
/// left (first stack window right, second left, third right, ...), each side stacked vertically
/// and sized to half the remaining width. With more than one master-only window and no stack,
/// they fill the whole width like plain master-stack. With exactly one window total, though, it
/// gets a centered box (mfact-sized on both axes, mango-style) instead of stretching fullscreen
/// -- centered-master should look centered even with nothing to flank the master column.
/// </summary>
public sealed class CenteredMasterLayout : ILayout
{
    public const string LayoutName = "centered-master";

    public string Name => LayoutName;
    public string Symbol => "|M|";

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

        if (n == 1)
        {
            // Centered horizontally only (mfact width, like the master column always is) --
            // full height, no vertical inset. Only the sides should look "centered"; top/bottom
            // stay flush with the work area same as every other layout.
            int w = (int)Math.Round(work.Width * mfact);
            int x = work.X + (work.Width - w) / 2;
            result[0] = Inset(new LayoutRect(x, work.Y, w, work.Height), gap);
            return result;
        }

        int masterCount = Math.Min(n, nmaster);
        int stackCount = n - masterCount;

        if (stackCount == 0)
        {
            StackVertically(result, 0, masterCount, work.X, work.Width, work, gap);
            return result;
        }

        int masterWidth = masterCount > 0 ? (int)Math.Round(work.Width * mfact) : 0;
        int sideWidth = (work.Width - masterWidth) / 2;
        int masterX = work.X + sideWidth;
        int leftX = work.X;
        int rightX = masterX + masterWidth;

        StackVertically(result, 0, masterCount, masterX, masterWidth, work, gap);

        // Stack windows alternate right, left, right, left, ... starting with right.
        var rightIndices = new List<int>();
        var leftIndices = new List<int>();
        for (int i = masterCount; i < n; i++)
        {
            if ((i - masterCount) % 2 == 0)
                rightIndices.Add(i);
            else
                leftIndices.Add(i);
        }

        StackVerticallyAt(result, rightIndices, rightX, sideWidth, work, gap);
        StackVerticallyAt(result, leftIndices, leftX, sideWidth, work, gap);

        return result;
    }

    private static void StackVertically(LayoutRect[] result, int startIndex, int count, int x, int width, LayoutRect work, int gap)
    {
        int y = work.Y;
        for (int i = 0; i < count; i++)
        {
            int h = (work.Height - (y - work.Y)) / (count - i);
            result[startIndex + i] = Inset(new LayoutRect(x, y, width, h), gap);
            y += h;
        }
    }

    private static void StackVerticallyAt(LayoutRect[] result, List<int> indices, int x, int width, LayoutRect work, int gap)
    {
        int y = work.Y;
        for (int k = 0; k < indices.Count; k++)
        {
            int h = (work.Height - (y - work.Y)) / (indices.Count - k);
            result[indices[k]] = Inset(new LayoutRect(x, y, width, h), gap);
            y += h;
        }
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
