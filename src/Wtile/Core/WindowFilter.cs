namespace Wtile.Core;

/// <summary>
/// Everything the manageable-window filter needs, captured as plain data so the filter itself
/// stays a pure function -- independently testable without any real HWND/Win32 state.
/// </summary>
public readonly record struct WindowSnapshot(
    string Title,
    string ClassName,
    bool IsVisible,
    bool IsTopLevel,
    bool HasOwner,
    bool IsToolWindow,
    bool IsAppWindow,
    bool IsCloaked);

/// <summary>
/// Decides whether a window should be tiled. This is the highest-risk, most-iterated piece of
/// any first Windows tiling WM: beyond visibility/style-bit checks, shell surfaces (taskbar,
/// desktop, XAML-island hosts, tooltips) must be explicitly excluded by class name, and UWP
/// windows hidden on another virtual desktop ("cloaked") must be excluded too.
/// </summary>
public static class WindowFilter
{
    private static readonly HashSet<string> ExcludedClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
        "Windows.UI.Core.CoreWindow",
        "MultitaskingViewFrame",
        "XamlExplorerHostIslandWindow",
        "ApplicationManager_DesktopShellWindow",
        "TaskListThumbnailWnd",
        "tooltips_class32",
        "IME",
    };

    public static bool IsManageable(in WindowSnapshot window)
    {
        if (!window.IsVisible || !window.IsTopLevel || window.IsCloaked)
            return false;

        if (ExcludedClassNames.Contains(window.ClassName))
            return false;

        // A window with an owner (e.g. a dialog) or the WS_EX_TOOLWINDOW style is normally
        // excluded, unless it explicitly opts back in via WS_EX_APPWINDOW.
        if (window.HasOwner && !window.IsAppWindow)
            return false;
        if (window.IsToolWindow && !window.IsAppWindow)
            return false;

        return true;
    }
}
