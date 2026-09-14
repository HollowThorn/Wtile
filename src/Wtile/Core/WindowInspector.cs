using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Layouts;

namespace Wtile.Core;

/// <summary>Reads window/monitor state via Win32 into the plain data types the rest of Core uses.</summary>
internal static unsafe class WindowInspector
{
    public static WindowSnapshot Describe(HWND hwnd)
    {
        bool isVisible = PInvoke.IsWindowVisible(hwnd);
        bool isTopLevel = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd;
        bool hasOwner = !PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull;

        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & (int)WINDOW_EX_STYLE.WS_EX_APPWINDOW) != 0;

        return new WindowSnapshot(
            Title: GetWindowText(hwnd),
            ClassName: GetClassName(hwnd),
            IsVisible: isVisible,
            IsTopLevel: isTopLevel,
            HasOwner: hasOwner,
            IsToolWindow: isToolWindow,
            IsAppWindow: isAppWindow,
            IsCloaked: IsCloaked(hwnd));
    }

    /// <summary>True if DWM is currently cloaking this window -- e.g. it's on another virtual
    /// desktop, or (some shell flyouts, like the clipboard-history/emoji panel) it was dismissed
    /// without ever being destroyed or Win32-hidden: IsWindowVisible stays true the whole time, so
    /// this is the only way to tell it's not actually on screen. See WindowManager.TiledWindowsOn,
    /// which excludes cloaked windows from tiling for exactly that reason.</summary>
    public static bool IsCloaked(HWND hwnd)
    {
        int cloaked = 0;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int));
        return hr.Succeeded && cloaked != 0;
    }

    public static string GetWindowText(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        int len = PInvoke.GetWindowText(hwnd, buffer);
        return len > 0 ? new string(buffer[..len]) : "";
    }

    private static string GetClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        int len = PInvoke.GetClassName(hwnd, buffer);
        return len > 0 ? new string(buffer[..len]) : "";
    }

    /// <summary>Resolves the executable file name (e.g. "notepad.exe") owning a window, for
    /// process-name blacklist matching. Uses PROCESS_QUERY_LIMITED_INFORMATION -- the minimal
    /// access right, which succeeds even against most elevated/protected processes -- and fails
    /// gracefully (returns false) rather than throwing on access-denied, so a protected process
    /// can never crash the WM.</summary>
    public static bool TryGetProcessName(HWND hwnd, out string processName)
    {
        processName = "";

        PInvoke.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return false;

        HANDLE process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsNull)
            return false;

        try
        {
            Span<char> buffer = stackalloc char[260]; // MAX_PATH
            uint size = (uint)buffer.Length;
            fixed (char* p = buffer)
            {
                if (!PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, new PWSTR(p), &size))
                    return false;
            }
            if (size == 0)
                return false;

            processName = Path.GetFileName(new string(buffer[..(int)size]));
            return processName.Length > 0;
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    public readonly record struct MonitorInfo(HMONITOR Handle, bool IsPrimary);

    private static List<MonitorInfo>? _enumerationBuffer;

    /// <summary>Enumerates every connected monitor. Only the handle/primary-ness is kept --
    /// bounds/work-area are always re-queried fresh per monitor at arrange time (see
    /// GetMonitorBounds/GetMonitorWorkArea) rather than cached, so a resolution change or a
    /// taskbar-hide toggle is picked up without needing separate cache invalidation.</summary>
    public static List<MonitorInfo> EnumerateMonitors()
    {
        _enumerationBuffer = [];
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, &MonitorEnumProc, default);
        List<MonitorInfo> result = _enumerationBuffer;
        _enumerationBuffer = null;
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL MonitorEnumProc(HMONITOR hMonitor, HDC hdc, RECT* lprcMonitor, LPARAM dwData)
    {
        MONITORINFO info = default;
        info.cbSize = (uint)sizeof(MONITORINFO);
        if (PInvoke.GetMonitorInfo(hMonitor, &info))
        {
            bool isPrimary = (info.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0;
            _enumerationBuffer?.Add(new MonitorInfo(hMonitor, isPrimary));
        }
        return true;
    }

    public static HMONITOR GetMonitorForWindow(HWND hwnd) =>
        PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

    public static LayoutRect GetMonitorWorkArea(HMONITOR handle) => GetMonitorInfo(handle, useWorkArea: true);

    /// <summary>Full monitor bounds, ignoring the taskbar's work-area reservation -- used when the
    /// taskbar is hidden (see TaskbarController) so tiling reclaims the space it would otherwise
    /// still exclude (hiding the taskbar's window doesn't withdraw its AppBar reservation).</summary>
    public static LayoutRect GetMonitorBounds(HMONITOR handle) => GetMonitorInfo(handle, useWorkArea: false);

    private static LayoutRect GetMonitorInfo(HMONITOR handle, bool useWorkArea)
    {
        MONITORINFO info = default;
        info.cbSize = (uint)sizeof(MONITORINFO);
        if (!PInvoke.GetMonitorInfo(handle, &info))
            return default;

        RECT r = useWorkArea ? info.rcWork : info.rcMonitor;
        return new LayoutRect(r.left, r.top, r.right - r.left, r.bottom - r.top);
    }

    public static int GetStyle(HWND hwnd) => PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

    /// <summary>
    /// Adds/removes WS_CAPTION and WS_THICKFRAME so the window is drawn without a title bar or
    /// resize border. SWP_FRAMECHANGED is required for DWM to
    /// actually recompute the non-client area; without it the style bits change but the window
    /// keeps rendering its old frame. <paramref name="originalStyle"/> should be the style
    /// captured via <see cref="GetStyle"/> before ever hiding it, so restoring (hidden: false)
    /// reproduces exactly what the window had -- not every window has WS_THICKFRAME to begin
    /// with (e.g. fixed-size dialogs), so blindly re-adding both bits would misrepresent some.
    /// </summary>
    public static void SetTitlebarHidden(HWND hwnd, int originalStyle, bool hidden)
    {
        int target = hidden
            ? originalStyle & ~(int)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME)
            : originalStyle;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, target);
        PInvoke.SetWindowPos(
            hwnd, HWND.Null, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Plain <c>SetForegroundWindow</c> is silently blocked by Windows' foreground-lock-timeout
    /// heuristic for any process that doesn't already "own" the current input focus grant --
    /// which a background WM reacting to a hotkey never does. Symptom without this: focus
    /// switches once, then gets stuck, with the target's taskbar button merely flashing instead
    /// of it actually activating. The standard fix (used by every third-party window-switcher)
    /// is to temporarily attach this thread's input queue to the foreground/target threads'
    /// queues via AttachThreadInput, which makes Windows treat the calls as if they came from an
    /// already-focused thread.
    /// </summary>
    public static void ForceSetForegroundWindow(HWND target)
    {
        HWND foreground = PInvoke.GetForegroundWindow();
        uint currentThreadId = PInvoke.GetCurrentThreadId();
        uint foregroundThreadId = PInvoke.GetWindowThreadProcessId(foreground, null);
        uint targetThreadId = PInvoke.GetWindowThreadProcessId(target, null);

        bool attachedForeground = foregroundThreadId != 0 && foregroundThreadId != currentThreadId
            && PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        bool attachedTarget = targetThreadId != 0 && targetThreadId != currentThreadId && targetThreadId != foregroundThreadId
            && PInvoke.AttachThreadInput(currentThreadId, targetThreadId, true);

        PInvoke.SetForegroundWindow(target);
        PInvoke.BringWindowToTop(target);

        if (attachedTarget)
            PInvoke.AttachThreadInput(currentThreadId, targetThreadId, false);
        if (attachedForeground)
            PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, false);
    }

    /// <summary>
    /// Ordinary top-level windows on Windows 10/11 have an invisible resize-handle border that
    /// GetWindowRect includes but that isn't actually painted on screen -- DWM's "extended frame
    /// bounds" is the true visible rect. Without compensating for the difference, tiled windows
    /// show a persistent ~7px margin on every side that's independent of the configured gap.
    /// Returns how much bigger than its visible bounds this window's actual rect is, per edge
    /// (usually 0 on top, a few px elsewhere); all zero if the DWM query fails.
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) GetInvisibleBorderInsets(HWND hwnd)
    {
        if (!PInvoke.GetWindowRect(hwnd, out RECT actual))
            return default;

        RECT visible;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(
            hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &visible, (uint)sizeof(RECT));
        if (!hr.Succeeded)
            return default;

        return (visible.left - actual.left, visible.top - actual.top, actual.right - visible.right, actual.bottom - visible.bottom);
    }

    /// <summary>The window's true on-screen rect (DWM's "extended frame bounds" -- see
    /// GetInvisibleBorderInsets), for drawing something (e.g. a focus border) flush against what
    /// the user actually sees rather than the wider GetWindowRect that includes the invisible
    /// resize-handle margin. Falls back to GetWindowRect if the DWM query fails.</summary>
    public static RECT GetVisibleBounds(HWND hwnd)
    {
        RECT visible;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(
            hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &visible, (uint)sizeof(RECT));
        if (hr.Succeeded)
            return visible;

        PInvoke.GetWindowRect(hwnd, out RECT actual);
        return actual;
    }
}
