using Windows.Win32.Graphics.Gdi;

namespace Wtile.Core;

/// <summary>
/// A connected monitor's own tag/layout state (dwm's per-monitor Monitor struct). Each monitor
/// tiles its own windows on its own active tag, entirely independent of every other monitor --
/// shared config (tag count, default layout params) seeds every monitor the same way at startup
/// or reload, but each then diverges independently at runtime, same as tags already do per-tag.
/// Bounds/work-area are deliberately not stored here -- see WindowInspector.GetMonitorBounds/
/// GetMonitorWorkArea, always queried fresh from <see cref="Handle"/> at arrange time.
/// </summary>
internal sealed class Monitor(HMONITOR handle, bool isPrimary, Tag[] tags)
{
    /// <summary>Settable, not fixed for the monitor's lifetime: an HMONITOR is only valid until
    /// the display configuration changes (waking from sleep, a resolution change, a monitor
    /// un/replugged), after which this monitor's tag/layout state is re-bound to the freshly
    /// enumerated handle in place -- see <see cref="WindowManager.RefreshMonitors"/>. Null while
    /// this monitor is not currently connected, in which case arranging it is skipped.</summary>
    public HMONITOR Handle { get; set; } = handle;
    public bool IsPrimary { get; set; } = isPrimary;
    public Tag[] Tags { get; set; } = tags;
    public int ActiveTagIndex { get; set; }
    public int PreviousTagIndex { get; set; }

    /// <summary>dwm's view(~0), scoped to this monitor: every one of its tags shown together.</summary>
    public bool IsViewingAllTags { get; set; }

    /// <summary>Set once Wtile has complained that this monitor reports no usable geometry, so
    /// the next arrange while it's still asleep/disconnected stays quiet -- cleared as soon as it
    /// tiles normally again. See WindowManager.ArrangeMonitor.</summary>
    public bool ReportedNoGeometry { get; set; }

    /// <summary>Height in pixels this monitor's own bar reserves at the top/bottom of its work area.</summary>
    public int ReservedTopInset { get; set; }
    public int ReservedBottomInset { get; set; }
}
