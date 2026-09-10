using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wtile.Core;

/// <summary>
/// Hides/shows the real Windows taskbar via ShowWindow -- distinct from Windows' own "auto-hide"
/// setting, which still reveals it on mouse-hover at the screen edge; this makes it not appear
/// at all, matching bug.n's taskbar-hide feature. Enumerates every instance of both the primary
/// (Shell_TrayWnd) and secondary-monitor (Shell_SecondaryTrayWnd, one per extra monitor) tray
/// windows via FindWindowEx, since FindWindow alone only returns the first match.
/// </summary>
internal static class TaskbarController
{
    public static void SetVisible(bool visible)
    {
        SHOW_WINDOW_CMD cmd = visible ? SHOW_WINDOW_CMD.SW_SHOW : SHOW_WINDOW_CMD.SW_HIDE;
        ToggleAll("Shell_TrayWnd", cmd);
        ToggleAll("Shell_SecondaryTrayWnd", cmd);
    }

    /// <summary>
    /// Whether the primary taskbar is currently visible. Used once at startup: if Wtile is
    /// (re)started while the taskbar happens to already be hidden -- e.g. a previous run hid it
    /// and never got to restore it before exiting -- the layout should recognize that and use the
    /// full monitor bounds from the start, rather than defaulting to "taskbar shown" and leaving
    /// its reserved work-area space unused.
    /// </summary>
    public static bool IsVisible()
    {
        HWND hwnd = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "Shell_TrayWnd", null);
        return hwnd.IsNull || PInvoke.IsWindowVisible(hwnd);
    }

    private static void ToggleAll(string className, SHOW_WINDOW_CMD cmd)
    {
        HWND hwnd = default;
        while (true)
        {
            hwnd = PInvoke.FindWindowEx(HWND.Null, hwnd, className, null);
            if (hwnd.IsNull)
                break;
            PInvoke.ShowWindow(hwnd, cmd);
        }
    }
}
