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
    public void MasterArea_MatchesMasterStackGeometry()
    {
        var deck = new DeckLayout();
        var masterStack = new MasterStackLayout();

        var deckResult = deck.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, mfact: 0.55)));
        var masterStackResult = masterStack.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, mfact: 0.55)));

        Assert.Equal(masterStackResult[0], deckResult[0]); // master rect identical to master-stack
    }

    [Fact]
    public void StackWindows_AllShareTheSameRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 4, Params(nmaster: 1)));

        Assert.Equal(4, result.Count);
        Assert.Equal(result[1], result[2]);
        Assert.Equal(result[1], result[3]);
        Assert.NotEqual(result[0], result[1]); // master rect is distinct from the stack rect
    }

    [Fact]
    public void StackRect_OccupiesRemainingWidthAfterMaster()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, mfact: 0.55)));

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.55);
        Assert.Equal(WorkArea.X + expectedMasterWidth, result[1].X);
        Assert.Equal(WorkArea.Width - expectedMasterWidth, result[1].Width);
        Assert.Equal(WorkArea.Height, result[1].Height);
    }

    [Fact]
    public void NmasterCoversAllWindows_NoStackRect()
    {
        var layout = new DeckLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 5)));

        Assert.All(result, r => Assert.Equal(WorkArea.Height, r.Height));
        Assert.Equal(WorkArea.Width, result[0].Width + result[1].Width);
    }

    [Fact]
    public void Gap_InsetsMasterAndStackRects()
    {
        var layout = new DeckLayout();
        int gap = 10;
        var noGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, gap: 0)));
        var withGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(nmaster: 1, gap: gap)));

        Assert.Equal(noGap[0].X + gap / 2, withGap[0].X);
        Assert.Equal(noGap[0].Width - gap, withGap[0].Width);
        Assert.Equal(noGap[1].X + gap / 2, withGap[1].X);
        Assert.Equal(noGap[1].Width - gap, withGap[1].Width);
    }
}
