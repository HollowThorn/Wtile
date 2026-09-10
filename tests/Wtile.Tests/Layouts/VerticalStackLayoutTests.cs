using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class VerticalStackLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056);

    private static IReadOnlyDictionary<string, double> Params(int gap = 0)
        => new Dictionary<string, double> { ["gap"] = gap };

    [Fact]
    public void NameAndSymbol()
    {
        var layout = new VerticalStackLayout();
        Assert.Equal("vertical", layout.Name);
        Assert.Equal("=", layout.Symbol);
    }

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new VerticalStackLayout();
        Assert.Empty(layout.Arrange(new LayoutContext(WorkArea, 0, Params())));
    }

    [Fact]
    public void SingleWindow_FillsEntireWorkArea()
    {
        var layout = new VerticalStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params()));

        Assert.Single(result);
        Assert.Equal(WorkArea, result[0]);
    }

    [Fact]
    public void ThreeWindows_FullWidthRowsStackedTopToBottom()
    {
        var layout = new VerticalStackLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 3, Params()));

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(WorkArea.X, r.X));
        Assert.All(result, r => Assert.Equal(WorkArea.Width, r.Width));

        Assert.Equal(WorkArea.Y, result[0].Y);
        Assert.Equal(result[0].Y + result[0].Height, result[1].Y);
        Assert.Equal(result[1].Y + result[1].Height, result[2].Y);
        Assert.Equal(WorkArea.Height, result[0].Height + result[1].Height + result[2].Height);
    }

    [Fact]
    public void Gap_InsetsEveryRectOnAllSides()
    {
        var layout = new VerticalStackLayout();
        int gap = 10;
        var noGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(gap: 0)));
        var withGap = layout.Arrange(new LayoutContext(WorkArea, 3, Params(gap: gap)));

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
        var layout = new VerticalStackLayout();
        for (int n = 1; n <= 8; n++)
        {
            var result = layout.Arrange(new LayoutContext(WorkArea, n, Params()));
            for (int i = 0; i < result.Count; i++)
                for (int j = i + 1; j < result.Count; j++)
                    Assert.False(Overlaps(result[i], result[j]), $"n={n}: rect {i} overlaps rect {j}");
        }
    }

    private static bool Overlaps(LayoutRect a, LayoutRect b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
}
