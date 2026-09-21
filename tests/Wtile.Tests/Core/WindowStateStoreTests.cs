using Wtile.Core;

namespace Wtile.Tests.Core;

public class WindowStateStoreTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"wtile-state-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveThenLoad_RoundTripsEverything()
    {
        string path = TempPath();
        try
        {
            var state = new SavedState
            {
                IsTaskbarHidden = true,
                Monitors = [new SavedMonitorState { Index = 0, ActiveTagIndex = 2, IsViewingAllTags = true }],
                Windows =
                [
                    new SavedWindowState
                    {
                        ProcessName = "chrome.exe",
                        ClassName = "Chrome_WidgetWin_1",
                        Title = "Example — Chrome",
                        MonitorIndex = 0,
                        TagIndex = 2,
                        IsFloating = true,
                        IsPinned = false,
                        OriginalStyle = 0x16CA0000,
                    },
                ],
            };

            WindowStateStore.Save(path, state);
            bool loaded = WindowStateStore.TryLoad(path, out SavedState result);

            Assert.True(loaded);
            Assert.True(result.IsTaskbarHidden);
            Assert.Single(result.Monitors);
            Assert.Equal(0, result.Monitors[0].Index);
            Assert.Equal(2, result.Monitors[0].ActiveTagIndex);
            Assert.True(result.Monitors[0].IsViewingAllTags);

            Assert.Single(result.Windows);
            SavedWindowState w = result.Windows[0];
            Assert.Equal("chrome.exe", w.ProcessName);
            Assert.Equal("Chrome_WidgetWin_1", w.ClassName);
            Assert.Equal("Example — Chrome", w.Title);
            Assert.Equal(0, w.MonitorIndex);
            Assert.Equal(2, w.TagIndex);
            Assert.True(w.IsFloating);
            Assert.False(w.IsPinned);
            Assert.Equal(0x16CA0000, w.OriginalStyle);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        string path = TempPath(); // never written
        bool loaded = WindowStateStore.TryLoad(path, out SavedState state);

        Assert.False(loaded);
        Assert.False(state.IsTaskbarHidden);
        Assert.Empty(state.Monitors);
        Assert.Empty(state.Windows);
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsFalseWithoutThrowing()
    {
        string path = TempPath();
        try
        {
            File.WriteAllText(path, "{ not valid json");

            bool loaded = WindowStateStore.TryLoad(path, out SavedState state);

            Assert.False(loaded);
            Assert.Empty(state.Monitors);
            Assert.Empty(state.Windows);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
