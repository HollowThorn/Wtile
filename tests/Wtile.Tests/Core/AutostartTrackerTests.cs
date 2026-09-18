using Wtile.Core;

namespace Wtile.Tests.Core;

public class AutostartTrackerTests
{
    [Fact]
    public void NoEntries_ConsumesNothing()
    {
        var tracker = new AutostartTracker([]);

        Assert.False(tracker.TryConsume("firefox.exe"));
        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void EachEntry_IsConsumedOnce_CaseInsensitively()
    {
        var tracker = new AutostartTracker(["firefox.exe", "WindowsTerminal.exe"]);

        Assert.True(tracker.TryConsume("FIREFOX.EXE"));
        Assert.False(tracker.TryConsume("firefox.exe")); // the second Firefox window follows normally
        Assert.Equal(1, tracker.PendingCount);

        Assert.True(tracker.TryConsume("windowsterminal.exe"));
        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void DuplicateEntries_AbsorbOneWindowEach()
    {
        var tracker = new AutostartTracker(["wt.exe", "wt.exe"]);

        Assert.True(tracker.TryConsume("wt.exe"));
        Assert.True(tracker.TryConsume("wt.exe"));
        Assert.False(tracker.TryConsume("wt.exe"));
    }

    [Fact]
    public void UnresolvedProcessName_NeverMatches()
    {
        var tracker = new AutostartTracker(["firefox.exe"]);

        Assert.False(tracker.TryConsume(""));
        Assert.Equal(1, tracker.PendingCount);
    }
}
