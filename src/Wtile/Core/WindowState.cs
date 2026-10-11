using System.Text.Json.Serialization;

namespace Wtile.Core;

// Plain mutable classes with simple typed properties only, same constraint as ConfigModel.cs --
// required shape for System.Text.Json's AOT source generator (WindowStateJsonContext below).
// Public (unlike most of Core) so WindowStateStore's JSON round-trip is unit-testable without
// InternalsVisibleTo, same reasoning that already makes WindowFilter/WindowSnapshot/ConfigLoader
// public.

public sealed class SavedState
{
    /// <summary>Whether the real Windows taskbar was hidden (see WindowManager.IsTaskbarHidden,
    /// toggle-taskbar) at the last save -- global, not per-monitor, same as the live flag it
    /// mirrors. Restored on startup/reload only; always shown again unconditionally at quit
    /// regardless of this (see Program.cs), so it's never left hidden behind if Wtile exits.</summary>
    public bool IsTaskbarHidden { get; set; }

    public List<SavedMonitorState> Monitors { get; set; } = [];
    public List<SavedWindowState> Windows { get; set; } = [];
}

public sealed class SavedMonitorState
{
    public int Index { get; set; }
    public int ActiveTagIndex { get; set; }
    public bool IsViewingAllTags { get; set; }
}

public sealed class SavedWindowState
{
    public string ProcessName { get; set; } = "";
    public string ClassName { get; set; } = "";

    /// <summary>Raw HWND value at capture time, as an opaque integer. HWNDs aren't stable across
    /// a genuine restart (the OS can recycle a destroyed window's handle for an unrelated one), so
    /// this is only ever trusted as an exact-match fast path when it still points at a live window
    /// whose process+class also still agree (see WindowStateMatcher) -- never trusted alone. 0 in
    /// state files saved before this existed, or whenever it fails to resolve; always safely falls
    /// through to the process+class heuristic below.</summary>
    public long Handle { get; set; }

    /// <summary>Captured for human-readability of state.json, and as a last-resort tie-breaker
    /// when two live windows share the same ProcessName+ClassName and neither has an exact Handle
    /// match (see WindowStateMatcher) -- never a primary matching key, since a window's title
    /// churns too often (e.g. browser tabs) to be trusted as a stable identity on its own.</summary>
    public string Title { get; set; } = "";

    public int MonitorIndex { get; set; }
    public int TagIndex { get; set; }
    public bool IsFloating { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>GWL_STYLE before any titlebar hiding, captured from ManagedWindow.OriginalStyle --
    /// lets a crashed hideTitlebars session's windows get their true decorations back, instead of
    /// a fresh instance adopting their already-stripped current style as the new baseline.</summary>
    public int OriginalStyle { get; set; }

    public bool? IsHiddenByWtile { get; set; } // null in state files saved before this existed
}

/// <summary>AOT-safe (reflection-free) JSON (de)serialization context for state.json -- mirrors
/// YamlContext.cs's role for config.yaml, but System.Text.Json's source generator only needs the
/// root type registered; it resolves reachable nested types/collections on its own.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SavedState))]
public partial class WindowStateJsonContext : JsonSerializerContext
{
}
