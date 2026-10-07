using Wtile.Core;
using Wtile.Layouts;

namespace Wtile.Tests.Core;

public class DropTargetTests
{
    private static readonly LayoutRect Tile = new(100, 100, 400, 200);

    [Theory]
    [InlineData(110, 200, false)] // left triangle
    [InlineData(490, 200, true)]  // right triangle
    [InlineData(300, 110, false)] // top triangle
    [InlineData(300, 290, true)]  // bottom triangle
    public void DropsAfterTile_FollowsTheTriangleTheCursorIsIn(int x, int y, bool expected)
    {
        Assert.Equal(expected, DropTarget.DropsAfterTile(Tile, x, y));
    }

    [Fact]
    public void DropsAfterTile_ScalesTheDiagonalsToAWideTile()
    {
        // Further right than down from center, yet still in the bottom triangle of a wide tile.
        Assert.True(DropTarget.DropsAfterTile(Tile, 430, 280));
        Assert.True(DropTarget.DropsAfterTile(Tile, 150, 290));
    }

    [Fact]
    public void DistanceSquaredToRect_IsZeroInside()
    {
        Assert.Equal(0, DropTarget.DistanceSquaredToRect(Tile, 300, 200));
    }

    [Fact]
    public void DistanceSquaredToRect_MeasuresToTheNearestEdge()
    {
        Assert.Equal(100, DropTarget.DistanceSquaredToRect(Tile, 90, 200));
        Assert.Equal(25 + 100, DropTarget.DistanceSquaredToRect(Tile, 504, 309));
    }
}
