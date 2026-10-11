using Wtile.Core;

namespace Wtile.Tests.Core;

public class WindowStateMatcherTests
{
    [Fact]
    public void Match_ExactHandle_WinsOverFifoOrderAndStaleTitle()
    {
        List<SavedWindowState> saved =
        [
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 100, Title = "Old Title A" },
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 200, Title = "Old Title B" },
        ];
        // Live list in reverse order relative to saved, and titles no longer match -- plain
        // FIFO-by-process+class, or title tie-break, would both get this wrong.
        List<LiveWindowSnapshot> live =
        [
            new LiveWindowSnapshot(200, "chrome.exe", "Chrome_WidgetWin_1", "New Title B"),
            new LiveWindowSnapshot(100, "chrome.exe", "Chrome_WidgetWin_1", "New Title A"),
        ];

        Dictionary<int, long> matches = WindowStateMatcher.Match(saved, live);

        Assert.Equal(100, matches[0]);
        Assert.Equal(200, matches[1]);
    }

    [Fact]
    public void Match_NoHandleInfo_TitleTieBreaksAmbiguousProcessClass()
    {
        List<SavedWindowState> saved =
        [
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 0, Title = "Page A" },
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 0, Title = "Page B" },
        ];
        List<LiveWindowSnapshot> live =
        [
            new LiveWindowSnapshot(200, "chrome.exe", "Chrome_WidgetWin_1", "Page B"),
            new LiveWindowSnapshot(100, "chrome.exe", "Chrome_WidgetWin_1", "Page A"),
        ];

        Dictionary<int, long> matches = WindowStateMatcher.Match(saved, live);

        Assert.Equal(100, matches[0]);
        Assert.Equal(200, matches[1]);
    }

    [Fact]
    public void Match_HandleStaleOrRecycled_FallsBackToTitleThenProcessClass()
    {
        List<SavedWindowState> saved =
        [
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 999, Title = "Page A" },
        ];
        List<LiveWindowSnapshot> live =
        [
            new LiveWindowSnapshot(100, "chrome.exe", "Chrome_WidgetWin_1", "Page A"),
        ];

        Dictionary<int, long> matches = WindowStateMatcher.Match(saved, live);

        Assert.Equal(100, matches[0]);
    }

    [Fact]
    public void Match_SameProcessClassAndTitle_AmbiguousStillMatchesDistinctHandlesWithoutCrashing()
    {
        List<SavedWindowState> saved =
        [
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 0, Title = "New Tab" },
            new SavedWindowState { ProcessName = "chrome.exe", ClassName = "Chrome_WidgetWin_1", Handle = 0, Title = "New Tab" },
        ];
        List<LiveWindowSnapshot> live =
        [
            new LiveWindowSnapshot(100, "chrome.exe", "Chrome_WidgetWin_1", "New Tab"),
            new LiveWindowSnapshot(200, "chrome.exe", "Chrome_WidgetWin_1", "New Tab"),
        ];

        Dictionary<int, long> matches = WindowStateMatcher.Match(saved, live);

        Assert.Equal(2, matches.Count);
        Assert.NotEqual(matches[0], matches[1]);
    }

    [Fact]
    public void Match_EmptyProcessName_NeverMatches()
    {
        List<SavedWindowState> saved =
        [
            new SavedWindowState { ProcessName = "", ClassName = "SomeClass", Handle = 100, Title = "Whatever" },
        ];
        List<LiveWindowSnapshot> live =
        [
            new LiveWindowSnapshot(100, "", "SomeClass", "Whatever"),
        ];

        Dictionary<int, long> matches = WindowStateMatcher.Match(saved, live);

        Assert.Empty(matches);
    }
}
