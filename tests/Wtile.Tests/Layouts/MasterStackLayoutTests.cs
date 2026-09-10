using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class MasterStackLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056); // below a 24px bar

    private static IReadOnlyDictionary<string, double> Params(int nmaster = 1, double mfact = 0.55, int gap = 0)
        => new Dictionary<string, double> { ["nmaster"] = nmaster, ["mfact"] = mfact, ["gap"] = gap };

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 0, Params()));
        Assert.Empty(result);
    }

    [Fact]
    public void SingleWindow_FillsEntireWorkArea()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params()));

        Assert.Single(result);
        Assert.Equal(WorkArea, result[0]);
    }

    [Fact]
    public void TwoWindows_DefaultNmaster1_SplitsByMfact()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 1, mfact: 0.55)));

        Assert.Equal(2, result.Count);

        var master = result[0];
        var stack = result[1];

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.55);
        Assert.Equal(WorkArea.X, master.X);
        Assert.Equal(WorkArea.Y, master.Y);
        Assert.Equal(expectedMasterWidth, master.Width);
        Assert.Equal(WorkArea.Height, master.Height);

        Assert.Equal(WorkArea.X + expectedMasterWidth, stack.X);
        Assert.Equal(WorkArea.Y, stack.Y);
        Assert.Equal(WorkArea.Width - expectedMasterWidth, stack.Width);
        Assert.Equal(WorkArea.Height, stack.Height);
    }

    [Fact]
    public void ThreeWindows_Nmaster1_StacksTwoInStackColumn()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1)));

        Assert.Equal(3, result.Count);

        // Master column: one window, full height.
        Assert.Equal(WorkArea.Height, result[0].Height);

        // Stack column: two windows sharing the height evenly, same X/Width, stacked vertically.
        Assert.Equal(result[1].X, result[2].X);
        Assert.Equal(result[1].Width, result[2].Width);
        Assert.Equal(result[1].Y + result[1].Height, result[2].Y);
        Assert.Equal(WorkArea.Height, result[1].Height + result[2].Height);
    }

    [Fact]
    public void NmasterCoversAllWindows_MastersAreSideBySideColumns()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 5)));

        // No stack column left over, so both windows are masters -- side by side, full height each.
        Assert.All(result, r => Assert.Equal(WorkArea.Height, r.Height));
        Assert.Equal(WorkArea.Width, result[0].Width + result[1].Width);
        Assert.Equal(result[0].X + result[0].Width, result[1].X);
    }

    [Fact]
    public void ThreeWindows_Nmaster2_MasterColumnsAreSideBySide_NotStacked()
    {
        // bug.n-style: unlike dwm, master windows sit side by side as columns, not stacked
        // in rows. 3 windows, nmaster=2 -> master area splits into two columns; only the stack
        // window (the 3rd) is on its own.
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 2, mfact: 0.5)));

        Assert.Equal(WorkArea.Height, result[0].Height);
        Assert.Equal(WorkArea.Height, result[1].Height);
        Assert.Equal(result[0].Width, result[1].Width);
        Assert.Equal(result[0].X + result[0].Width, result[1].X); // side by side, not stacked

        Assert.Equal(result[1].X + result[1].Width, result[2].X);
        Assert.Equal(WorkArea.Height, result[2].Height);
    }

    [Fact]
    public void MasterOnRight_ThreeWindows_Nmaster2_MatchesBugNExample()
    {
        // The exact scenario described: "=[]" (master on right), 3 windows, nmaster raised to 2
        // -> three vertical strips; leftmost is the lone stack window, the two on the right are
        // side-by-side master columns.
        var layout = new MasterStackLayout(masterOnRight: true);
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 2, mfact: 0.5)));

        Assert.Equal(WorkArea.X, result[2].X); // stack window is leftmost
        Assert.Equal(result[2].X + result[2].Width, result[0].X);
        Assert.Equal(result[0].X + result[0].Width, result[1].X);
        Assert.Equal(WorkArea.Height, result[0].Height);
        Assert.Equal(WorkArea.Height, result[1].Height);
    }

    [Fact]
    public void NmasterZero_AllWindowsInStackColumn_FillsFullWidth()
    {
        var layout = new MasterStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 0)));

        Assert.All(result, r => Assert.Equal(WorkArea.X, r.X));
        Assert.All(result, r => Assert.Equal(WorkArea.Width, r.Width));
    }

    [Fact]
    public void Gap_InsetsEveryRectOnAllSides()
    {
        var layout = new MasterStackLayout();
        int gap = 10;
        var noGap = layout.Arrange(new LayoutContext(WorkArea, 2, Params(gap: 0)));
        var withGap = layout.Arrange(new LayoutContext(WorkArea, 2, Params(gap: gap)));

        for (int i = 0; i < noGap.Count; i++)
        {
            Assert.Equal(noGap[i].X + gap / 2, withGap[i].X);
            Assert.Equal(noGap[i].Y + gap / 2, withGap[i].Y);
            Assert.Equal(noGap[i].Width - gap, withGap[i].Width);
            Assert.Equal(noGap[i].Height - gap, withGap[i].Height);
        }
    }

    [Fact]
    public void RectanglesNeverOverlap_ForVariousWindowCounts()
    {
        var layout = new MasterStackLayout();
        for (int n = 1; n <= 8; n++)
        {
            var result = layout.Arrange(new LayoutContext(WorkArea, n, Params(nmaster: 2)));
            for (int i = 0; i < result.Count; i++)
                for (int j = i + 1; j < result.Count; j++)
                    Assert.False(Overlaps(result[i], result[j]), $"n={n}: rect {i} overlaps rect {j}");
        }
    }

    private static bool Overlaps(LayoutRect a, LayoutRect b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    [Fact]
    public void MasterOnRight_HasDistinctNameAndSymbol()
    {
        var layout = new MasterStackLayout(masterOnRight: true);
        Assert.Equal("master-stack-right", layout.Name);
        Assert.Equal("=[]", layout.Symbol);
        Assert.Equal("master-stack", new MasterStackLayout().Name);
        Assert.Equal("[]=", new MasterStackLayout().Symbol);
    }

    [Fact]
    public void MasterOnRight_TwoWindows_MasterIsOnTheRightSide()
    {
        var layout = new MasterStackLayout(masterOnRight: true);
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 1, mfact: 0.55)));

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.55);
        var master = result[0]; // index 0 is always master (regardless of which side it renders on)
        var stack = result[1];

        Assert.Equal(WorkArea.X, stack.X);
        Assert.Equal(WorkArea.Width - expectedMasterWidth, stack.Width);
        Assert.Equal(WorkArea.X + stack.Width, master.X);
        Assert.Equal(expectedMasterWidth, master.Width);
    }

    [Fact]
    public void MasterOnRight_SingleWindow_StillFillsEntireWorkArea()
    {
        var layout = new MasterStackLayout(masterOnRight: true);
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params()));

        Assert.Single(result);
        Assert.Equal(WorkArea, result[0]);
    }
}
