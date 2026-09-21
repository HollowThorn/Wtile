using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class DwindleLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056); // below a 24px bar

    private static IReadOnlyDictionary<string, double> Params(int nmaster = 1, double mfact = 0.55, int gap = 0)
        => new Dictionary<string, double> { ["nmaster"] = nmaster, ["mfact"] = mfact, ["gap"] = gap };

    [Fact]
    public void NameAndSymbol()
    {
        var layout = new DwindleLayout();
        Assert.Equal("dwindle", layout.Name);
        Assert.Equal("[\\]", layout.Symbol);
    }

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new DwindleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 0, Params()));
        Assert.Empty(result);
    }

    [Fact]
    public void SingleWindow_FillsEntireWorkArea()
    {
        var layout = new DwindleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params()));

        Assert.Single(result);
        Assert.Equal(WorkArea, result[0]);
    }

    [Fact]
    public void TwoWindows_SplitsAtMfact_SecondWindowGetsFullRemainder()
    {
        var layout = new DwindleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(mfact: 0.55)));

        int expectedFirstWidth = (int)Math.Round(WorkArea.Width * 0.55);
        Assert.Equal(WorkArea.X, result[0].X);
        Assert.Equal(WorkArea.Y, result[0].Y);
        Assert.Equal(expectedFirstWidth, result[0].Width);
        Assert.Equal(WorkArea.Height, result[0].Height);

        Assert.Equal(WorkArea.X + expectedFirstWidth, result[1].X);
        Assert.Equal(WorkArea.Y, result[1].Y);
        Assert.Equal(WorkArea.Width - expectedFirstWidth, result[1].Width);
        Assert.Equal(WorkArea.Height, result[1].Height);
    }

    [Fact]
    public void ThreeWindows_SpiralsLeftColumn_TopRight_BottomRight()
    {
        var layout = new DwindleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params(mfact: 0.5)));

        int leftWidth = (int)Math.Round(WorkArea.Width * 0.5);
        int rightWidth = WorkArea.Width - leftWidth;
        int rightX = WorkArea.X + leftWidth;
        int topHeight = (int)Math.Round(WorkArea.Height * 0.5);
        int bottomHeight = WorkArea.Height - topHeight;

        Assert.Equal(new LayoutRect(WorkArea.X, WorkArea.Y, leftWidth, WorkArea.Height), result[0]);
        Assert.Equal(new LayoutRect(rightX, WorkArea.Y, rightWidth, topHeight), result[1]);
        Assert.Equal(new LayoutRect(rightX, WorkArea.Y + topHeight, rightWidth, bottomHeight), result[2]);
    }

    [Fact]
    public void FourWindows_AlternatesAxisEachStep()
    {
        var layout = new DwindleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 4, Params(mfact: 0.5)));

        // The 3rd split (i=2, vertical again) happens within the bottom-right remainder from
        // ThreeWindows_SpiralsLeftColumn_TopRight_BottomRight -- result[2] should be its left
        // half, result[3] its right half (same Y/Height, split X).
        Assert.Equal(result[2].Y, result[3].Y);
        Assert.Equal(result[2].Height, result[3].Height);
        Assert.Equal(result[2].X + result[2].Width, result[3].X);
    }

    [Fact]
    public void NmasterParamIsIgnored()
    {
        var layout = new DwindleLayout();
        var withNmaster1 = layout.Arrange(new LayoutContext(WorkArea, 4, Params(nmaster: 1)));
        var withNmaster5 = layout.Arrange(new LayoutContext(WorkArea, 4, Params(nmaster: 5)));

        Assert.Equal(withNmaster1, withNmaster5);
    }

    [Fact]
    public void Gap_InsetsEveryRect()
    {
        var layout = new DwindleLayout();
        int gap = 10;
        var noGap = layout.Arrange(new LayoutContext(WorkArea, 4, Params(gap: 0)));
        var withGap = layout.Arrange(new LayoutContext(WorkArea, 4, Params(gap: gap)));

        for (int i = 0; i < noGap.Count; i++)
        {
            Assert.Equal(noGap[i].X + gap / 2, withGap[i].X);
            Assert.Equal(noGap[i].Y + gap / 2, withGap[i].Y);
            Assert.Equal(noGap[i].Width - gap, withGap[i].Width);
            Assert.Equal(noGap[i].Height - gap, withGap[i].Height);
        }
    }

    [Fact]
    public void NoOverlapAndFullCoverage_ForVariousWindowCounts()
    {
        var layout = new DwindleLayout();
        for (int n = 1; n <= 8; n++)
        {
            var result = layout.Arrange(new LayoutContext(WorkArea, n, Params(gap: 0)));

            for (int i = 0; i < result.Count; i++)
                for (int j = i + 1; j < result.Count; j++)
                    Assert.False(Overlaps(result[i], result[j]), $"n={n}: rect {i} overlaps rect {j}");

            long totalArea = 0;
            foreach (var r in result)
                totalArea += (long)r.Width * r.Height;
            Assert.Equal((long)WorkArea.Width * WorkArea.Height, totalArea);
        }
    }

    private static bool Overlaps(LayoutRect a, LayoutRect b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
}
