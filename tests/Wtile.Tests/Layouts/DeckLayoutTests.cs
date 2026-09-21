using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class DeckLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056); // below a 24px bar

    private static IReadOnlyDictionary<string, double> Params(int nmaster = 1, double mfact = 0.55, int gap = 0)
        => new Dictionary<string, double> { ["nmaster"] = nmaster, ["mfact"] = mfact, ["gap"] = gap };

    [Fact]
    public void NameAndSymbol()
    {
        var layout = new DeckLayout();
        Assert.Equal("deck", layout.Name);
        Assert.Equal("[D]", layout.Symbol);
    }

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 0, Params()));
        Assert.Empty(result);
    }

    [Fact]
    public void SingleWindow_FillsEntireWorkArea()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params()));

        Assert.Single(result);
        Assert.Equal(WorkArea, result[0]);
    }

    [Fact]
    public void TwoWindows_DefaultNmaster1_MasterGetsMfactWidth_StackGetsFullStackRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 1, mfact: 0.55)));

        Assert.Equal(2, result.Count);

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.55);
        Assert.Equal(WorkArea.X, result[0].X);
        Assert.Equal(expectedMasterWidth, result[0].Width);
        Assert.Equal(WorkArea.Height, result[0].Height);

        Assert.Equal(WorkArea.X + expectedMasterWidth, result[1].X);
        Assert.Equal(WorkArea.Width - expectedMasterWidth, result[1].Width);
        Assert.Equal(WorkArea.Height, result[1].Height);
    }

    [Fact]
    public void ThreeWindows_Nmaster1_AllStackWindowsShareIdenticalRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1)));

        Assert.Equal(3, result.Count);
        Assert.Equal(result[1], result[2]); // deck-style overlap, not stacked rows
    }

    [Fact]
    public void FiveWindows_Nmaster2_AllThreeStackWindowsShareIdenticalRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 5, Params(nmaster: 2)));

        Assert.Equal(5, result.Count);
        Assert.Equal(result[2], result[3]);
        Assert.Equal(result[2], result[4]);
    }

    [Fact]
    public void NmasterCoversAllWindows_MastersAreSideBySideColumns_NoStack()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 5)));

        Assert.All(result, r => Assert.Equal(WorkArea.Height, r.Height));
        Assert.Equal(WorkArea.Width, result[0].Width + result[1].Width);
        Assert.Equal(result[0].X + result[0].Width, result[1].X);
    }

    [Fact]
    public void ThreeWindows_Nmaster2_MasterColumnsSideBySide_LoneStackGetsFullStackRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 2, mfact: 0.5)));

        Assert.Equal(WorkArea.Height, result[0].Height);
        Assert.Equal(WorkArea.Height, result[1].Height);
        Assert.Equal(result[0].Width, result[1].Width);
        Assert.Equal(result[0].X + result[0].Width, result[1].X); // side by side

        Assert.Equal(result[1].X + result[1].Width, result[2].X);
        Assert.Equal(WorkArea.Height, result[2].Height);
    }

    [Fact]
    public void NmasterZero_AllWindowsShareFullWorkAreaAsStack()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 0)));

        Assert.All(result, r => Assert.Equal(WorkArea, r));
    }

    [Fact]
    public void Gap_InsetsMasterColumnAndStackRectConsistently()
    {
        var layout = new DeckLayout();
        int gap = 10;
        var noGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, gap: 0)));
        var withGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, gap: gap)));

        for (int i = 0; i < noGap.Count; i++)
        {
            Assert.Equal(noGap[i].X + gap / 2, withGap[i].X);
            Assert.Equal(noGap[i].Y + gap / 2, withGap[i].Y);
            Assert.Equal(noGap[i].Width - gap, withGap[i].Width);
            Assert.Equal(noGap[i].Height - gap, withGap[i].Height);
        }
    }

    [Fact]
    public void MasterColumnsNeverOverlapEachOtherOrStackRect_ForVariousWindowCounts()
    {
        var layout = new DeckLayout();
        for (int n = 1; n <= 8; n++)
        {
            var result = layout.Arrange(new LayoutContext(WorkArea, n, Params(nmaster: 2)));
            int masterCount = Math.Min(n, 2);

            // Master columns pairwise disjoint, and disjoint from every stack rect.
            for (int i = 0; i < masterCount; i++)
                for (int j = i + 1; j < result.Count; j++)
                    Assert.False(Overlaps(result[i], result[j]), $"n={n}: rect {i} overlaps rect {j}");

            // Stack rects, if any, must all be equal to each other (intended overlap).
            for (int i = masterCount; i < result.Count; i++)
                Assert.Equal(result[masterCount], result[i]);
        }
    }

    private static bool Overlaps(LayoutRect a, LayoutRect b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
}
