using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class MonocleLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056);

    [Fact]
    public void NameAndSymbol()
    {
        var layout = new MonocleLayout();
        Assert.Equal("monocle", layout.Name);
        Assert.Equal("[M]", layout.Symbol);
    }

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new MonocleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 0, new Dictionary<string, double>()));
        Assert.Empty(result);
    }

    [Fact]
    public void EveryWindow_GetsTheFullWorkArea()
    {
        var layout = new MonocleLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 4, new Dictionary<string, double>()));

        Assert.Equal(4, result.Count);
        Assert.All(result, r => Assert.Equal(WorkArea, r));
    }
}
