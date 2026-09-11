using Wtile.Core;

namespace Wtile.Tests.Core;

public class WindowFilterTests
{
    private static WindowSnapshot NormalApp(string className = "MyApp", string title = "My App") => new(
        Title: title,
        ClassName: className,
        IsVisible: true,
        IsTopLevel: true,
        HasOwner: false,
        IsToolWindow: false,
        IsAppWindow: false,
        IsCloaked: false);

    [Fact]
    public void NormalTopLevelWindow_IsManageable()
    {
        Assert.True(WindowFilter.IsManageable(NormalApp()));
    }

    [Fact]
    public void InvisibleWindow_IsNotManageable()
    {
        var w = NormalApp() with { IsVisible = false };
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void NonTopLevelWindow_IsNotManageable()
    {
        var w = NormalApp() with { IsTopLevel = false };
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void CloakedWindow_IsNotManageable()
    {
        // e.g. a UWP window hidden because it's on another virtual desktop.
        var w = NormalApp() with { IsCloaked = true };
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Worker Window")]
    [InlineData("Windows.UI.Core.CoreWindow")]
    [InlineData("tooltips_class32")]
    [InlineData("NativeHWNDHost")]
    public void KnownShellClasses_AreNotManageable(string className)
    {
        var w = NormalApp(className);
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void OwnedWindowWithoutAppWindowStyle_IsNotManageable()
    {
        // e.g. a dialog owned by another window.
        var w = NormalApp() with { HasOwner = true, IsAppWindow = false };
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void OwnedWindowWithAppWindowStyle_IsManageable()
    {
        var w = NormalApp() with { HasOwner = true, IsAppWindow = true };
        Assert.True(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void ToolWindowWithoutAppWindowStyle_IsNotManageable()
    {
        var w = NormalApp() with { IsToolWindow = true, IsAppWindow = false };
        Assert.False(WindowFilter.IsManageable(w));
    }

    [Fact]
    public void ToolWindowWithAppWindowStyle_IsManageable()
    {
        var w = NormalApp() with { IsToolWindow = true, IsAppWindow = true };
        Assert.True(WindowFilter.IsManageable(w));
    }
}
