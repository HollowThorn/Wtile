using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class CenteredMasterLayoutTests
{
    private static readonly LayoutRect WorkArea = new(0, 24, 1920, 1056);

    private static IReadOnlyDictionary<string, double> Params(int nmaster = 1, double mfact = 0.5, int gap = 0)
        => new Dictionary<string, double> { ["nmaster"] = nmaster, ["mfact"] = mfact, ["gap"] = gap };

    [Fact]
    public void NameAndSymbol()
    {
        var layout = new CenteredMasterLayout();
        Assert.Equal("centered-master", layout.Name);
        Assert.Equal("|M|", layout.Symbol);
    }

    [Fact]
    public void NoWindows_ReturnsEmpty()
    {
        var layout = new CenteredMasterLayout();
        Assert.Empty(layout.Arrange(new LayoutContext(WorkArea, 0, Params())));
    }

    [Fact]
    public void SingleWindow_IsHorizontallyCenteredWithFullHeight()
    {
        var layout = new CenteredMasterLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 1, Params(mfact: 0.5)));

        Assert.Single(result);
        LayoutRect r = result[0];

        int expectedWidth = (int)Math.Round(WorkArea.Width * 0.5);
        Assert.Equal(expectedWidth, r.Width);
        Assert.Equal(WorkArea.Height, r.Height); // full height -- not vertically centered
        Assert.Equal(WorkArea.Y, r.Y);

        // Centered horizontally -- not stretched fullscreen like master-stack/monocle would.
        Assert.NotEqual(WorkArea.Width, r.Width);
        Assert.Equal(WorkArea.X + (WorkArea.Width - expectedWidth) / 2, r.X);
    }

    [Fact]
    public void TwoWindows_MasterCentered_FirstStackGoesRight()
    {
        var layout = new CenteredMasterLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 1, mfact: 0.5)));

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.5);
        int sideWidth = (WorkArea.Width - expectedMasterWidth) / 2;

        var master = result[0];
        var stack = result[1];

        Assert.Equal(WorkArea.X + sideWidth, master.X);
        Assert.Equal(expectedMasterWidth, master.Width);
        Assert.Equal(WorkArea.Height, master.Height);

        // First (and only) stack window goes to the right of master.
        Assert.Equal(master.X + master.Width, stack.X);
        Assert.Equal(sideWidth, stack.Width);
        Assert.Equal(WorkArea.Height, stack.Height);
    }

    [Fact]
    public void FiveWindows_Nmaster1_StackAlternatesRightThenLeft()
    {
        var layout = new CenteredMasterLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 5, Params(nmaster: 1, mfact: 0.5)));

        Assert.Equal(5, result.Count);

        int expectedMasterWidth = (int)Math.Round(WorkArea.Width * 0.5);
        int sideWidth = (WorkArea.Width - expectedMasterWidth) / 2;
        int masterX = WorkArea.X + sideWidth;
        int rightX = masterX + expectedMasterWidth;
        int leftX = WorkArea.X;

        // index 0 = master; 1,3 = right (2 windows sharing the right column); 2,4 = left.
        Assert.Equal(masterX, result[0].X);
        Assert.Equal(rightX, result[1].X);
        Assert.Equal(leftX, result[2].X);
        Assert.Equal(rightX, result[3].X);
        Assert.Equal(leftX, result[4].X);

        // Each side's two windows share the full height between them.
        Assert.Equal(WorkArea.Height, result[1].Height + result[3].Height);
        Assert.Equal(WorkArea.Height, result[2].Height + result[4].Height);
    }

    [Fact]
    public void NoStack_MasterFillsFullWidth()
    {
        var layout = new CenteredMasterLayout();
        var result = layout.Arrange(new LayoutContext(WorkArea, 2, Params(nmaster: 5)));

        Assert.All(result, r => Assert.Equal(WorkArea.Width, r.Width));
        Assert.Equal(WorkArea.Height, result[0].Height + result[1].Height);
    }

    [Fact]
    public void RectanglesNeverOverlap_ForVariousWindowCounts()
    {
        var layout = new CenteredMasterLayout();
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
}
