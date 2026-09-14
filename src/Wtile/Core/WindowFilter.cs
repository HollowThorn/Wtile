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
        "Worker Window", // explorer.exe's desktop/wallpaper host -- recreated on a display change
                         // (e.g. a VM resizing the guest's resolution), briefly appearing as a
                         // plain untitled top-level window before Explorer re-parents/cloaks it
        "Windows.UI.Core.CoreWindow",
        "ApplicationFrameWindow", // UWP app host (Settings, Calculator, Photos, Mail, Maps, ...) --
                                  // bug.n leaves these untiled by default too; the host resizes its
                                  // inner CoreWindow on its own schedule, fighting external WinAPI resizes
        "MultitaskingViewFrame",
        "XamlExplorerHostIslandWindow",
        "ApplicationManager_DesktopShellWindow",
        "TaskListThumbnailWnd",
        "tooltips_class32",
        "IME",
        "NativeHWNDHost", // legacy volume/brightness OSD flyout host (stable since Vista)
        "#32770", // generic Windows dialog-box template (Run, Open/Save, message boxes, property
                  // sheets, ...) -- almost always excluded already via HasOwner below, but a few
                  // (e.g. the Run dialog) deliberately set WS_EX_APPWINDOW despite having an owner
                  // just to force their own taskbar button, which otherwise opts them back into
                  // tiling. bug.n never tiles these either (they're WS_POPUP by construction).
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
