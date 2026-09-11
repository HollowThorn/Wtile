using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Layouts;

namespace Wtile.Core;

/// <summary>
/// Owns the live set of manageable windows across every monitor, each monitor's own per-tag
/// layout state, and focus tracking; drives arrangement. Only ever mutated on the thread that
/// runs the WinEventHook message pump (see Program.cs), so no locking is needed. A single
/// process-wide instance is expected: native callbacks (EnumWindows, WinEventProc) reach it via
/// <see cref="Current"/> since UnmanagedCallersOnly methods can't capture instance state.
/// </summary>
internal sealed unsafe class WindowManager
{
    public static WindowManager? Current { get; private set; }

    private readonly List<ManagedWindow> _windows = [];
    private readonly HashSet<HWND> _selfHidden = []; // hidden by ActivateTag, not a real close/hide
    private readonly LayoutRegistry _layouts;
    private readonly string _defaultLayoutName;
    private readonly IReadOnlyDictionary<string, double> _defaultLayoutParams;
    private IReadOnlyList<CompiledBlacklistRule> _blacklist = [];
    private bool _blacklistNeedsProcessName;
    private List<Monitor> _monitors = [];

    public WindowManager(LayoutRegistry layouts, string defaultLayoutName, IReadOnlyDictionary<string, double> defaultLayoutParams, int tagCount = 9)
    {
        _layouts = layouts;
        _defaultLayoutName = defaultLayoutName;
        _defaultLayoutParams = defaultLayoutParams;
        TagCount = tagCount;
        Current = this;
    }

    /// <summary>
    /// Enumerates connected monitors and builds independent tag state for each, seeded from the
    /// same shared config. Call once at startup, before Seed(). Re-enumerating later isn't
    /// supported yet -- a monitor count change (true hotplug) needs a restart; "reload" only
    /// refreshes geometry for monitors already known.
    /// </summary>
    public void InitializeMonitors()
    {
        List<WindowInspector.MonitorInfo> infos = WindowInspector.EnumerateMonitors();
        if (infos.Count == 0)
            throw new InvalidOperationException("EnumDisplayMonitors returned no monitors.");

        _monitors = infos.ConvertAll(info => new Monitor(info.Handle, info.IsPrimary, BuildTags(TagCount, _defaultLayoutName, _defaultLayoutParams)));

        int primaryIndex = _monitors.FindIndex(m => m.IsPrimary);
        CurrentMonitorIndex = Math.Max(0, primaryIndex);
    }

    private static Tag[] BuildTags(int count, string layoutName, IReadOnlyDictionary<string, double> layoutParams)
    {
        var tags = new Tag[count];
        for (int i = 0; i < count; i++)
            tags[i] = new Tag(layoutName, layoutParams);
        return tags;
    }

    public IReadOnlyList<ManagedWindow> Windows => _windows;
    public IReadOnlyList<Monitor> Monitors => _monitors;

    public int TagCount { get; private set; }

    /// <summary>dwm's selmon: which monitor hotkey commands (focus-next, view-tag, adjust-mfact,
    /// ...) target. Follows the focused window's monitor (see OnForegroundChanged).</summary>
    public int CurrentMonitorIndex { get; private set; }
    private Monitor CurrentMonitor => _monitors[CurrentMonitorIndex];
    private Tag CurrentTag => CurrentMonitor.Tags[CurrentMonitor.ActiveTagIndex];

    public HWND FocusedHandle { get; private set; }
    public string FocusedTitle { get; private set; } = "";
    public bool IsFocusedFloating => Find(FocusedHandle)?.IsFloating ?? false;

    public string GetLayoutSymbol(int monitorIndex)
    {
        Monitor monitor = _monitors[monitorIndex];
        return _layouts.TryGet(monitor.Tags[monitor.ActiveTagIndex].LayoutName, out ILayout layout) ? layout.Symbol : "?";
    }

    /// <summary>True while the real Windows taskbar has been hidden via toggle-taskbar (see
    /// TaskbarController); tiling then uses full monitor bounds instead of the work area. One
    /// global flag -- toggle-taskbar hides/shows every monitor's taskbar presence together.</summary>
    public bool IsTaskbarHidden { get; private set; }

    /// <summary>Global config-driven flag (general.hideTitlebars): strips WS_CAPTION/WS_THICKFRAME
    /// from every managed window, tiled and floating alike. See <see cref="SetHideTitlebars"/>.</summary>
    public bool HideTitlebars { get; private set; }

    /// <summary>Global config-driven flag (general.rememberLayout): whether Program.cs should
    /// save/restore window placement via WindowStateStore at startup/quit/reload. Off by default;
    /// a plain flag with no side effects, same shape as <see cref="SetBlacklist"/>.</summary>
    public bool RememberLayout { get; private set; }

    /// <summary>Fires after any state change any bar might need to redraw for (arrange, focus, tag switch).</summary>
    public event Action? Changed;

    public bool HasWindowsOnTag(int monitorIndex, int tagIndex) =>
        _windows.Exists(w => w.MonitorIndex == monitorIndex && (w.TagIndex == tagIndex || w.IsPinned));

    /// <summary>Call once at startup, before the first Arrange(): if the real taskbar is already
    /// hidden (e.g. a previous run hid it and exited before restoring it), recognize that instead
    /// of defaulting to "shown" and leaving its reserved space unused.</summary>
    public void SyncInitialTaskbarState() => IsTaskbarHidden = !TaskbarController.IsVisible();

    public void ToggleTaskbar()
    {
        IsTaskbarHidden = !IsTaskbarHidden;
        TaskbarController.SetVisible(!IsTaskbarHidden);
        Arrange();
    }

    /// <summary>Applies (or lifts) title-bar hiding across every currently-tracked window --
    /// called from config load/reload with general.hideTitlebars, and with false at shutdown so
    /// windows get their decorations back even if Wtile exits while the flag was on.</summary>
    /// <summary>Replaces the live set of blacklist rules (see WindowBlacklist). Only affects
    /// windows opened after this call -- one already being managed is not retroactively released
    /// (TryAdd, where the check runs, is a no-op for an already-tracked window). No lock needed:
    /// WindowManager is only ever touched on the single WinEventHook pump thread, same reasoning
    /// as SetHideTitlebars.</summary>
    public void SetBlacklist(IReadOnlyList<CompiledBlacklistRule> rules)
    {
        _blacklist = rules;
        _blacklistNeedsProcessName = rules.Any(r => r.ProcessName is not null);
    }

    public void SetRememberLayout(bool enabled) => RememberLayout = enabled;

    public void SetHideTitlebars(bool hidden)
    {
        HideTitlebars = hidden;
        foreach (ManagedWindow w in _windows)
            if (!IsTitlebarHideExempt(w.ClassName))
                WindowInspector.SetTitlebarHidden(w.Handle, w.OriginalStyle, hidden);
        Arrange(); // window rects need re-sending: the frame changed, so invisible-border insets did too
    }

    /// <summary>Legacy console host windows (cmd.exe/powershell.exe under conhost.exe, class
    /// "ConsoleWindowClass" -- not Windows Terminal, which is a normal window) compute their
    /// buffer/window size relative to their own non-client frame internally; stripping
    /// WS_CAPTION/WS_THICKFRAME externally leaves them sized or positioned wrong (observed:
    /// window shifted off to one side). Excluded from title-bar hiding unconditionally rather
    /// than working around conhost's own layout math.</summary>
    private static bool IsTitlebarHideExempt(string className) => className == "ConsoleWindowClass";

    /// <summary>Resets every monitor's every tag layout to the given name/params from a reloaded
    /// config, discarding any per-tag runtime divergence (e.g. from adjust-mfact) -- consistent
    /// with how reload overwrites every other piece of live state. Tag count/default layout are
    /// shared config across monitors; each monitor's own occupancy/active-tag stays untouched.</summary>
    public void ResetAllTagLayoutParams(string layoutName, IReadOnlyDictionary<string, double> layoutParams)
    {
        foreach (Monitor monitor in _monitors)
        {
            foreach (Tag tag in monitor.Tags)
            {
                tag.LayoutName = layoutName;
                tag.LayoutParams = new Dictionary<string, double>(layoutParams);
                tag.RememberedGap = layoutParams.TryGetValue("gap", out double g) && g > 0 ? g : null;
            }
        }
        Arrange();
    }

    /// <summary>
    /// Applies a new tag count (shared across every monitor) from a reloaded config. Windows
    /// whose tag is no longer valid are clamped onto the last tag rather than orphaned.
    /// </summary>
    public void UpdateTagCount(int tagCount)
    {
        if (tagCount == TagCount || tagCount < 1)
            return;
        TagCount = tagCount;

        foreach (Monitor monitor in _monitors)
        {
            Tag template = monitor.Tags[0];
            var newTags = new Tag[tagCount];
            for (int i = 0; i < tagCount; i++)
                newTags[i] = i < monitor.Tags.Length ? monitor.Tags[i] : new Tag(template.LayoutName, template.LayoutParams);
            monitor.Tags = newTags;

            if (monitor.ActiveTagIndex >= tagCount)
                monitor.ActiveTagIndex = tagCount - 1;
        }

        foreach (ManagedWindow w in _windows)
        {
            if (w.TagIndex >= tagCount)
                w.TagIndex = tagCount - 1;
        }

        Arrange();
    }

    /// <summary>Populates the initial window set from already-open windows, then arranges.</summary>
    public void Seed()
    {
        PInvoke.EnumWindows(&EnumWindowsProc, 0);
        Arrange();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL EnumWindowsProc(HWND hwnd, LPARAM lParam)
    {
        Current?.TryAdd(hwnd, arrange: false);
        return true;
    }

    /// <summary>Snapshots which monitor/tag every currently-tracked window is on, for
    /// WindowStateStore to write to state.json. Only called at quit/reload (see Program.cs/
    /// ReloadCommand), so resolving each window's process name here (a syscall per window) is
    /// cheap enough -- never done on the hot add/remove/arrange path.</summary>
    public SavedState CaptureState()
    {
        var state = new SavedState();

        for (int i = 0; i < _monitors.Count; i++)
        {
            Monitor monitor = _monitors[i];
            state.Monitors.Add(new SavedMonitorState
            {
                Index = i,
                ActiveTagIndex = monitor.ActiveTagIndex,
                IsViewingAllTags = monitor.IsViewingAllTags,
            });
        }

        foreach (ManagedWindow w in _windows)
        {
            WindowInspector.TryGetProcessName(w.Handle, out string processName);
            state.Windows.Add(new SavedWindowState
            {
                ProcessName = processName,
                ClassName = w.ClassName,
                Title = w.Title,
                MonitorIndex = w.MonitorIndex,
                TagIndex = w.TagIndex,
                IsFloating = w.IsFloating,
                IsPinned = w.IsPinned,
            });
        }

        return state;
    }

    /// <summary>Restores monitor/tag placement from a previously captured state (see
    /// <see cref="CaptureState"/>), matching saved windows to currently-tracked live ones since
    /// HWNDs aren't stable across a restart. Matching is best-effort by process name + window
    /// class (title excluded -- it churns, e.g. browser tabs): saved windows are matched in saved
    /// order, FIFO, against not-yet-claimed live windows; an unmatched saved record is dropped,
    /// and a live window with no matching record just keeps whatever placement Seed()/TryAdd
    /// already gave it. Called at startup and from ReloadCommand -- never on the hot path.</summary>
    public void ApplySavedState(SavedState state)
    {
        foreach (SavedMonitorState saved in state.Monitors)
        {
            if (saved.Index < 0 || saved.Index >= _monitors.Count)
                continue;
            Monitor monitor = _monitors[saved.Index];
            monitor.ActiveTagIndex = Math.Clamp(saved.ActiveTagIndex, 0, TagCount - 1);
            monitor.IsViewingAllTags = saved.IsViewingAllTags;
        }

        var liveProcessNames = new Dictionary<HWND, string>();
        foreach (ManagedWindow w in _windows)
        {
            WindowInspector.TryGetProcessName(w.Handle, out string processName);
            liveProcessNames[w.Handle] = processName;
        }

        var claimed = new HashSet<HWND>();
        var restored = new List<ManagedWindow>();
        foreach (SavedWindowState saved in state.Windows)
        {
            if (string.IsNullOrEmpty(saved.ProcessName))
                continue; // never matches -- avoids false positives between two access-denied windows

            ManagedWindow? match = _windows.Find(w =>
                !claimed.Contains(w.Handle) && w.ClassName == saved.ClassName && liveProcessNames[w.Handle] == saved.ProcessName);
            if (match is null)
                continue;

            claimed.Add(match.Handle);
            match.MonitorIndex = Math.Clamp(saved.MonitorIndex, 0, _monitors.Count - 1);
            match.TagIndex = Math.Clamp(saved.TagIndex, 0, TagCount - 1);
            match.IsFloating = saved.IsFloating;
            match.IsPinned = saved.IsPinned;
            restored.Add(match);
        }

        foreach (ManagedWindow w in _windows)
        {
            if (!claimed.Contains(w.Handle))
                restored.Add(w);
        }

        _windows.Clear();
        _windows.AddRange(restored);

        ResyncVisibility();
        Arrange();
    }

    /// <summary>Shows/hides every tracked window to match what IsVisibleOn now says for its
    /// (possibly just-reassigned) monitor/tag -- Arrange() alone only repositions the tiled set,
    /// it doesn't show/hide anything, and every window ApplySavedState touches was already
    /// OS-visible (WindowFilter.IsManageable requires that). Same per-window show/hide + _selfHidden
    /// bookkeeping ActivateTag already does, just generalized across every monitor at once.</summary>
    private void ResyncVisibility()
    {
        foreach (ManagedWindow w in _windows)
        {
            Monitor monitor = _monitors[w.MonitorIndex];
            if (IsVisibleOn(w, monitor, w.MonitorIndex))
            {
                _selfHidden.Remove(w.Handle);
                PInvoke.ShowWindow(w.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            }
            else
            {
                _selfHidden.Add(w.Handle);
                PInvoke.ShowWindow(w.Handle, SHOW_WINDOW_CMD.SW_HIDE);
            }
        }
    }

    public void OnWindowShown(HWND hwnd) => TryAdd(hwnd, arrange: true);

    private const nuint RearrangeTimerId = 1;

    /// <summary>Schedules a one-shot re-arrange after delayMs, coalescing repeated calls (same
    /// hWnd/id resets the pending timer rather than stacking). Used right after adding a window
    /// via EVENT_OBJECT_UNCLOAKED (see WinEventTracker): that first Arrange() can race ahead of
    /// the app settling its own geometry, or of DWM's extended-frame-bounds (used to compensate
    /// for the invisible resize border, see GetInvisibleBorderInsets) catching up to the
    /// just-uncloaked window -- observed as Firefox opening at the wrong size/position, spilling
    /// under the bar, until something else (e.g. switching layouts) forces a fresh Arrange().
    /// This follow-up corrects it automatically instead of requiring that manual nudge.</summary>
    public void ScheduleRearrange(uint delayMs) =>
        PInvoke.SetTimer(HWND.Null, RearrangeTimerId, delayMs, &RearrangeTimerProc);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void RearrangeTimerProc(HWND hwnd, uint msg, nuint idEvent, uint dwTime)
    {
        PInvoke.KillTimer(HWND.Null, idEvent);
        Current?.Arrange();
    }

    public void OnWindowHidden(HWND hwnd)
    {
        if (_selfHidden.Remove(hwnd))
            return; // we hid this ourselves for a tag switch; it's still tracked

        if (Remove(hwnd))
            Arrange();
    }

    public void OnWindowDestroyed(HWND hwnd)
    {
        _selfHidden.Remove(hwnd);
        if (Remove(hwnd))
            Arrange();
    }

    public void OnMinimizeChanged(HWND hwnd, bool minimized)
    {
        ManagedWindow? window = Find(hwnd);
        if (window is null || window.IsMinimized == minimized)
            return;
        window.IsMinimized = minimized;
        Arrange();
    }

    /// <summary>DWM can cloak a tracked window -- a virtual-desktop switch away from it, or (some
    /// shell flyouts, like the clipboard-history/emoji panel opened via Win+V/Win+.) it being
    /// dismissed -- without ever firing EVENT_OBJECT_HIDE or EVENT_OBJECT_DESTROY, since
    /// IsWindowVisible stays true the whole time. Left unhandled, a cloaked window stays counted
    /// as tiled forever: an invisible gap in the layout that nothing ever fills. Re-arranging here
    /// makes TiledWindowsOn's IsCloaked check (which already excludes it) take effect immediately,
    /// rather than waiting for some unrelated future Arrange() to happen to skip it. Deliberately
    /// does NOT untrack the window (unlike OnWindowHidden/OnWindowDestroyed) -- a virtual-desktop
    /// cloak is not a close, and EVENT_OBJECT_UNCLOAKED already re-surfaces it (via OnWindowShown)
    /// with its placement intact when it uncloaks again.</summary>
    public void OnWindowCloaked(HWND hwnd)
    {
        if (Find(hwnd) is not null)
            Arrange();
    }

    /// <summary>Fires once a drag/resize of the focused window finishes (see
    /// EVENT_SYSTEM_MOVESIZEEND in WinEventTracker) so the focus border can snap to its new
    /// position. Deliberately NOT routed through Changed/Arrange -- Arrange() would fight the
    /// user's own drag by re-tiling everything else.</summary>
    public event Action? FocusMoved;

    public void OnWindowMoved(HWND hwnd)
    {
        if (hwnd == FocusedHandle)
            FocusMoved?.Invoke();
    }

    /// <summary>Windows to skip when tracking focus, e.g. Wtile's own bars (they can briefly
    /// report foreground on creation despite WS_EX_NOACTIVATE).</summary>
    public HashSet<HWND> IgnoredFocusHandles { get; } = [];

    public void OnForegroundChanged(HWND hwnd)
    {
        if (IgnoredFocusHandles.Contains(hwnd))
            return;

        // Belt-and-suspenders fallback for WinEventTracker's EVENT_OBJECT_UNCLOAKED handler
        // (the correctly-timed fix for apps whose main window appears via a DWM "uncloak" rather
        // than a fresh SW_SHOW, e.g. Firefox): if a window somehow still isn't tracked by the
        // time it's focused, give it one more chance here rather than leaving it stuck forever
        // (subject to the same manageable-window filter as everything else). Idempotent -- TryAdd
        // is a no-op if EVENT_OBJECT_UNCLOAKED already picked it up, which is the common case.
        if (Find(hwnd) is null)
            TryAdd(hwnd, arrange: true);

        FocusedHandle = hwnd;
        FocusedTitle = WindowInspector.GetWindowText(hwnd);

        // dwm's selmon follows focus: hotkey commands should target whichever monitor the
        // window you just focused is actually on.
        ManagedWindow? window = Find(hwnd);
        if (window is not null)
            CurrentMonitorIndex = window.MonitorIndex;

        Changed?.Invoke();
    }

    public void OnTitleChanged(HWND hwnd)
    {
        ManagedWindow? window = Find(hwnd);
        if (window is not null)
            window.Title = WindowInspector.GetWindowText(hwnd);

        if (hwnd == FocusedHandle)
        {
            FocusedTitle = WindowInspector.GetWindowText(hwnd);
            Changed?.Invoke();
        }
    }

    /// <summary>Moves focus to the next/previous tiled window in stack order (dwm's focusstack), on the current monitor.</summary>
    public void FocusNext() => FocusRelative(+1);
    public void FocusPrev() => FocusRelative(-1);

    private void FocusRelative(int direction)
    {
        List<ManagedWindow> tiled = TiledWindowsOnCurrentMonitor();
        if (tiled.Count == 0)
            return;
        int currentIndex = tiled.FindIndex(w => w.Handle == FocusedHandle);
        int nextIndex = currentIndex < 0 ? 0 : ((currentIndex + direction) % tiled.Count + tiled.Count) % tiled.Count;
        WindowInspector.ForceSetForegroundWindow(tiled[nextIndex].Handle);
    }

    /// <summary>dwm's zoom: master swaps with the next window; anything else becomes the new master.</summary>
    public void SwapMaster()
    {
        ManagedWindow? focused = Find(FocusedHandle);
        if (focused is null || focused.IsFloating || !IsVisibleOn(focused, CurrentMonitor, CurrentMonitorIndex))
            return;

        List<ManagedWindow> tiled = TiledWindowsOnCurrentMonitor();
        if (tiled.Count < 2)
            return;

        ManagedWindow master = tiled[0];
        ManagedWindow target = ReferenceEquals(focused, master) ? tiled[1] : focused;

        int i = _windows.IndexOf(master);
        int j = _windows.IndexOf(target);
        (_windows[i], _windows[j]) = (_windows[j], _windows[i]);

        Arrange();
    }

    /// <summary>bug.n's shuffleWindow: swaps the focused window with its immediate neighbor in
    /// stack order (direction +1/-1), letting you reorder the whole stack incrementally rather
    /// than just jumping to/from master like <see cref="SwapMaster"/>.</summary>
    public void ShuffleWindow(int direction)
    {
        ManagedWindow? focused = Find(FocusedHandle);
        if (focused is null || focused.IsFloating || !IsVisibleOn(focused, CurrentMonitor, CurrentMonitorIndex))
            return;

        List<ManagedWindow> tiled = TiledWindowsOnCurrentMonitor();
        int index = tiled.IndexOf(focused);
        int swapWith = index + direction;
        if (index < 0 || swapWith < 0 || swapWith >= tiled.Count)
            return;

        ManagedWindow other = tiled[swapWith];
        int i = _windows.IndexOf(focused);
        int j = _windows.IndexOf(other);
        (_windows[i], _windows[j]) = (_windows[j], _windows[i]);

        Arrange();
    }

    /// <summary>Moves the focused window to another tag on its own monitor, dropping it there;
    /// hides it if that tag isn't the active one. Always unpins first -- moving a pinned window
    /// to a tag is how you "drop" it out of pin mode onto that tag, rather than it staying
    /// pinned everywhere.</summary>
    public void MoveWindowToTag(int tagIndex)
    {
        if (tagIndex < 0 || tagIndex >= TagCount)
            return;
        ManagedWindow? window = Find(FocusedHandle);
        if (window is null || (!window.IsPinned && window.TagIndex == tagIndex))
            return;

        window.IsPinned = false;
        window.TagIndex = tagIndex;
        if (tagIndex == _monitors[window.MonitorIndex].ActiveTagIndex)
        {
            _selfHidden.Remove(window.Handle);
            PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
        else
        {
            _selfHidden.Add(window.Handle);
            PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        }

        Arrange();
    }

    /// <summary>Excludes/re-includes the focused window from tiling; it keeps whatever rect it had.</summary>
    public void ToggleFloating()
    {
        ManagedWindow? window = Find(FocusedHandle);
        if (window is null)
            return;
        window.IsFloating = !window.IsFloating;
        Arrange();
    }

    /// <summary>Nudges the active tag's mfact by <paramref name="delta"/>, clamped to [0.05, 0.95].
    /// Only affects the current monitor's active tag -- other tags keep whatever mfact they last
    /// had. With master on the right (master-stack's fixed edge flips sides), the sign is
    /// flipped so the hotkey keeps the same spatial feel either way -- growing master always
    /// visually pushes the master/stack divider the same direction its key suggests, not
    /// backwards once master's on the other side.</summary>
    public void AdjustMfact(double delta)
    {
        Tag tag = CurrentTag;
        if (tag.LayoutName == MasterStackLayout.RightLayoutName)
            delta = -delta;
        double current = tag.LayoutParams.TryGetValue("mfact", out double v) ? v : 0.55;
        tag.LayoutParams["mfact"] = Math.Clamp(current + delta, 0.05, 0.95);
        Arrange();
    }

    /// <summary>Nudges the active tag's nmaster (master-area window count) by <paramref name="delta"/>, floored at 0.</summary>
    public void AdjustNmaster(double delta)
    {
        Tag tag = CurrentTag;
        double current = tag.LayoutParams.TryGetValue("nmaster", out double v) ? v : 1;
        tag.LayoutParams["nmaster"] = Math.Max(0, current + delta);
        Arrange();
    }

    /// <summary>Switches the active tag to a different registered layout by name (dwm's setlayout);
    /// its existing per-tag params (nmaster/mfact/gap) carry over unchanged. An unknown name is
    /// logged and ignored (see Arrange()) rather than throwing.</summary>
    public void SetLayout(string layoutName)
    {
        CurrentTag.LayoutName = layoutName;
        Arrange();
    }

    /// <summary>Nudges the active tag's gap by <paramref name="delta"/>, floored at 0. A resulting
    /// non-zero value becomes the new toggle-gap "remembered" value.</summary>
    public void AdjustGap(double delta)
    {
        Tag tag = CurrentTag;
        double current = tag.LayoutParams.TryGetValue("gap", out double v) ? v : 0;
        double updated = Math.Max(0, current + delta);
        tag.LayoutParams["gap"] = updated;
        if (updated > 0)
            tag.RememberedGap = updated;
        Arrange();
    }

    /// <summary>dwm's togglegaps: zero the active tag's gap, or restore whatever it was before it was last zeroed.</summary>
    public void ToggleGap()
    {
        Tag tag = CurrentTag;
        double current = tag.LayoutParams.TryGetValue("gap", out double v) ? v : 0;
        tag.LayoutParams["gap"] = current > 0 ? 0 : (tag.RememberedGap ?? 0);
        Arrange();
    }

    /// <summary>Politely asks the focused window to close (WM_CLOSE), same as clicking its close button.</summary>
    public void CloseFocusedWindow()
    {
        if (!FocusedHandle.IsNull)
            PInvoke.PostMessage(FocusedHandle, PInvoke.WM_CLOSE, 0, 0);
    }

    /// <summary>Forcibly terminates the process owning the focused window (TerminateProcess) --
    /// for a hung or non-cooperating window that ignores kill-window's WM_CLOSE (some shell
    /// flyouts/system dialogs never process it at all). Destructive: unlike WM_CLOSE, the target
    /// gets no chance to prompt or save, so it's a separate command/hotkey rather than folded into
    /// kill-window itself.</summary>
    public void ForceCloseFocusedWindow()
    {
        if (FocusedHandle.IsNull)
            return;

        PInvoke.GetWindowThreadProcessId(FocusedHandle, out uint pid);
        if (pid == 0)
            return;

        HANDLE process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE, false, pid);
        if (process.IsNull)
            return;
        try
        {
            PInvoke.TerminateProcess(process, 1);
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    private List<ManagedWindow> TiledWindowsOnCurrentMonitor() => TiledWindowsOn(CurrentMonitor, CurrentMonitorIndex);

    private List<ManagedWindow> TiledWindowsOn(Monitor monitor, int monitorIndex) =>
        _windows.FindAll(w => IsVisibleOn(w, monitor, monitorIndex) && !w.IsMinimized && !w.IsFloating && !WindowInspector.IsCloaked(w.Handle));

    /// <summary>True if this window is currently supposed to be visible on this specific monitor:
    /// on its active tag, pinned (bug.n's "pin" -- visible/tiled on every tag regardless of its
    /// own TagIndex, but not across monitors), or every tag is while
    /// <see cref="Monitor.IsViewingAllTags"/> (dwm's view(~0)). Pinned windows never cross
    /// monitors -- MonitorIndex must already match.</summary>
    internal static bool IsVisibleOn(ManagedWindow w, Monitor monitor, int monitorIndex) =>
        w.MonitorIndex == monitorIndex && (monitor.IsViewingAllTags || w.TagIndex == monitor.ActiveTagIndex || w.IsPinned);

    /// <summary>Pins/unpins the focused window so it stays visible across every tag switch on its
    /// own monitor, tiled alongside whatever tag is currently active there (bug.n's "pin").</summary>
    public void TogglePinned()
    {
        ManagedWindow? window = Find(FocusedHandle);
        if (window is null)
            return;
        window.IsPinned = !window.IsPinned;

        Monitor monitor = _monitors[window.MonitorIndex];
        // Unpinning while away from the window's own tag: it was only visible by virtue of being
        // pinned, so it needs to actually disappear now, not just stop being tiled -- unless
        // we're viewing all tags, in which case everything stays visible regardless.
        if (!window.IsPinned && !monitor.IsViewingAllTags && window.TagIndex != monitor.ActiveTagIndex)
        {
            _selfHidden.Add(window.Handle);
            PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        }

        Arrange();
    }

    /// <summary>Views the tag <paramref name="delta"/> positions away (wrapping), e.g. dwm's/bug.n's shiftview.</summary>
    public void ShiftView(int delta) => ActivateTag(((CurrentMonitor.ActiveTagIndex + delta) % TagCount + TagCount) % TagCount);

    /// <summary>Switches back to whichever tag was active before the current one on this monitor
    /// (dwm's/bug.n's "view previous"). A no-op the very first time, before any tag switch has happened.</summary>
    public void ToggleLastTag() => ActivateTag(CurrentMonitor.PreviousTagIndex);

    /// <summary>dwm's view(~0): shows every tag's windows on the current monitor together instead
    /// of just the active tag's. Switching to any specific tag (ActivateTag) exits this again.</summary>
    public void ViewAllTags()
    {
        Monitor monitor = CurrentMonitor;
        int monitorIndex = CurrentMonitorIndex;
        if (monitor.IsViewingAllTags)
            return;
        monitor.IsViewingAllTags = true;

        foreach (ManagedWindow w in _windows)
        {
            if (w.MonitorIndex == monitorIndex && w.TagIndex != monitor.ActiveTagIndex && !w.IsPinned)
            {
                _selfHidden.Remove(w.Handle);
                PInvoke.ShowWindow(w.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            }
        }

        Arrange();
        Changed?.Invoke();
    }

    /// <summary>Switches the current monitor's visible tag: hides windows that shouldn't be
    /// visible anymore, shows ones that should, arranges the new set. Also exits view-all-tags
    /// mode on this monitor if it was active. Other monitors are untouched.</summary>
    public void ActivateTag(int tagIndex)
    {
        if (tagIndex < 0 || tagIndex >= TagCount)
            return;
        Monitor monitor = CurrentMonitor;
        int monitorIndex = CurrentMonitorIndex;
        if (tagIndex == monitor.ActiveTagIndex && !monitor.IsViewingAllTags)
            return;

        bool wasViewingAll = monitor.IsViewingAllTags;
        monitor.IsViewingAllTags = false;
        int previous = monitor.ActiveTagIndex;
        monitor.PreviousTagIndex = previous;
        monitor.ActiveTagIndex = tagIndex;

        foreach (ManagedWindow w in _windows)
        {
            if (w.MonitorIndex != monitorIndex)
                continue;

            bool wasVisible = wasViewingAll || w.TagIndex == previous || w.IsPinned;
            bool shouldBeVisible = w.TagIndex == tagIndex || w.IsPinned;

            if (wasVisible && !shouldBeVisible)
            {
                _selfHidden.Add(w.Handle);
                PInvoke.ShowWindow(w.Handle, SHOW_WINDOW_CMD.SW_HIDE);
            }
            else if (!wasVisible && shouldBeVisible)
            {
                PInvoke.ShowWindow(w.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            }
        }

        Arrange();
        Changed?.Invoke();
    }

    /// <summary>Sets which monitor hotkey commands target, without touching Win32 focus -- used
    /// by a bar tag-click on a non-current monitor (dwm-style: clicking a tag on another
    /// monitor's bar selects that monitor first, then switches its tag).</summary>
    public void SelectMonitor(int monitorIndex)
    {
        if (monitorIndex >= 0 && monitorIndex < _monitors.Count)
            CurrentMonitorIndex = monitorIndex;
    }

    /// <summary>dwm's focusmon: switches which monitor hotkey commands target, wrapping around,
    /// and moves real Win32 focus to whatever's tiled there (if anything).</summary>
    public void FocusMonitor(int delta)
    {
        if (_monitors.Count < 2)
            return;
        CurrentMonitorIndex = ((CurrentMonitorIndex + delta) % _monitors.Count + _monitors.Count) % _monitors.Count;

        List<ManagedWindow> tiled = TiledWindowsOnCurrentMonitor();
        if (tiled.Count > 0)
            WindowInspector.ForceSetForegroundWindow(tiled[0].Handle);

        Changed?.Invoke();
    }

    /// <summary>dwm's tagmon: moves the focused window to the monitor <paramref name="delta"/>
    /// away, dropping it onto that monitor's currently active tag (unpinning it first, same as
    /// move-window-to-tag).</summary>
    public void MoveWindowToMonitor(int delta)
    {
        if (_monitors.Count < 2)
            return;
        ManagedWindow? window = Find(FocusedHandle);
        if (window is null)
            return;

        int targetIndex = ((window.MonitorIndex + delta) % _monitors.Count + _monitors.Count) % _monitors.Count;
        if (targetIndex == window.MonitorIndex)
            return;

        Monitor target = _monitors[targetIndex];
        window.IsPinned = false;
        window.MonitorIndex = targetIndex;
        window.TagIndex = target.ActiveTagIndex;
        _selfHidden.Remove(window.Handle);
        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

        CurrentMonitorIndex = targetIndex;
        Arrange();
    }

    private void TryAdd(HWND hwnd, bool arrange)
    {
        if (Find(hwnd) is not null)
            return; // idempotent: a single user action can fire several hook events for one window

        WindowSnapshot snapshot = WindowInspector.Describe(hwnd);
        if (!WindowFilter.IsManageable(snapshot))
        {
            // Only titled windows -- most filtered-out windows are untitled shell/helper surfaces
            // and would otherwise drown this out. Diagnoses "app X doesn't tile" reports: shows
            // exactly which check rejected it instead of guessing.
            if (!string.IsNullOrWhiteSpace(snapshot.Title))
            {
                Console.WriteLine($"[filter] Skipped '{snapshot.Title}' (class={snapshot.ClassName}) -- "
                    + $"visible={snapshot.IsVisible} topLevel={snapshot.IsTopLevel} hasOwner={snapshot.HasOwner} "
                    + $"toolWindow={snapshot.IsToolWindow} appWindow={snapshot.IsAppWindow} cloaked={snapshot.IsCloaked}");
            }
            return;
        }

        if (_blacklist.Count > 0)
        {
            string processName = _blacklistNeedsProcessName && WindowInspector.TryGetProcessName(hwnd, out string name) ? name : "";
            if (WindowBlacklist.IsBlacklisted(_blacklist, processName, snapshot.ClassName, snapshot.Title))
            {
                Console.WriteLine($"[blacklist] Skipped '{snapshot.Title}' (process='{processName}', class='{snapshot.ClassName}')");
                return;
            }
        }

        int monitorIndex = ResolveMonitorIndex(hwnd);
        Monitor monitor = _monitors[monitorIndex];

        var window = new ManagedWindow(hwnd)
        {
            Title = snapshot.Title,
            ClassName = snapshot.ClassName,
            MonitorIndex = monitorIndex,
            TagIndex = monitor.ActiveTagIndex,
            OriginalStyle = WindowInspector.GetStyle(hwnd),
        };
        Console.WriteLine($"[manage] '{window.Title}' (class='{window.ClassName}')");
        _windows.Insert(0, window); // dwm-style: a new window becomes master
        if (HideTitlebars && !IsTitlebarHideExempt(window.ClassName))
            WindowInspector.SetTitlebarHidden(hwnd, window.OriginalStyle, hidden: true);
        if (arrange)
            Arrange();
    }

    private int ResolveMonitorIndex(HWND hwnd)
    {
        HMONITOR handle = WindowInspector.GetMonitorForWindow(hwnd);
        int index = _monitors.FindIndex(m => m.Handle == handle);
        return index >= 0 ? index : CurrentMonitorIndex;
    }

    private bool Remove(HWND hwnd)
    {
        int index = _windows.FindIndex(w => w.Handle == hwnd);
        if (index < 0)
            return false;
        _windows.RemoveAt(index);
        return true;
    }

    private ManagedWindow? Find(HWND hwnd) => _windows.Find(w => w.Handle == hwnd);

    /// <summary>Re-tiles every monitor against its own current geometry and active tag. Called
    /// after any state change (window add/remove/move, tag switch, config reload, manual
    /// refresh) -- always picks up the current screen size per monitor, so a resolution change
    /// is corrected the next time anything triggers an arrange (e.g. the "reload" command).</summary>
    public void Arrange()
    {
        for (int i = 0; i < _monitors.Count; i++)
            ArrangeMonitor(i);
        Changed?.Invoke();
    }

    private void ArrangeMonitor(int monitorIndex)
    {
        Monitor monitor = _monitors[monitorIndex];
        List<ManagedWindow> tiled = TiledWindowsOn(monitor, monitorIndex);
        if (tiled.Count == 0)
            return;

        Tag activeTag = monitor.Tags[monitor.ActiveTagIndex];
        if (!_layouts.TryGet(activeTag.LayoutName, out ILayout layout))
        {
            Console.WriteLine($"[layout] Unknown layout '{activeTag.LayoutName}' for tag {monitor.ActiveTagIndex + 1} on monitor {monitorIndex + 1}; skipping arrange.");
            return;
        }

        LayoutRect workArea = IsTaskbarHidden
            ? WindowInspector.GetMonitorBounds(monitor.Handle)
            : WindowInspector.GetMonitorWorkArea(monitor.Handle);
        workArea = workArea with
        {
            Y = workArea.Y + monitor.ReservedTopInset,
            Height = workArea.Height - monitor.ReservedTopInset - monitor.ReservedBottomInset,
        };
        IReadOnlyList<LayoutRect> rects = layout.Arrange(new LayoutContext(workArea, tiled.Count, activeTag.LayoutParams));

        HDWP hdwp = PInvoke.BeginDeferWindowPos(tiled.Count);
        for (int i = 0; i < tiled.Count; i++)
        {
            HWND handle = tiled[i].Handle;
            LayoutRect r = rects[i];

            // Some apps ignore SetWindowPos while maximized; clear that state first.
            if (PInvoke.IsZoomed(handle))
                PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_RESTORE);

            // Compensate for the invisible resize border (see GetInvisibleBorderInsets) so the
            // window's *visible* bounds match the layout rect exactly, not its outer window rect.
            (int insetLeft, int insetTop, int insetRight, int insetBottom) = WindowInspector.GetInvisibleBorderInsets(handle);
            hdwp = PInvoke.DeferWindowPos(
                hdwp, handle, HWND.Null,
                r.X - insetLeft, r.Y - insetTop, r.Width + insetLeft + insetRight, r.Height + insetTop + insetBottom,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        }
        PInvoke.EndDeferWindowPos(hdwp);
    }
}
