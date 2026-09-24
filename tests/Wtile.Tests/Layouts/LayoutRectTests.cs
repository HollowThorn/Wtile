using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class LayoutRectTests
{
    [Fact]
    public void NormalRect_IsNotEmpty()
    {
        Assert.False(new LayoutRect(0, 0, 1920, 1080).IsEmpty);
    }

    [Fact]
    public void OffOriginRect_IsNotEmpty()
    {
        Assert.False(new LayoutRect(-1920, 200, 1280, 1024).IsEmpty);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]    // what GetMonitorInfo failing on a stale handle yields
    [InlineData(0, 0, 1920, 0)]
    [InlineData(0, 0, 0, 1080)]
    [InlineData(0, 0, -10, -10)] // bar insets can over-subtract a tiny work area
    public void DegenerateRect_IsEmpty(int x, int y, int width, int height)
    {
        Assert.True(new LayoutRect(x, y, width, height).IsEmpty);
    }
}
