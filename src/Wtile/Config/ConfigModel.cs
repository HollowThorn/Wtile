namespace Wtile.Config;

// Plain mutable classes with simple typed properties only (no records, no structs, no free-form
// Dictionary<object,object?> maps) -- required shape for YamlDotNet's AOT source generator
// (Vecc.YamlDotNet.Analyzers.StaticGenerator), see YamlContext.cs.

public sealed class WtileConfig
{
    public GeneralConfig General { get; set; } = new();
    public List<LayoutConfig> Layouts { get; set; } = [];
    public BarConfig Bar { get; set; } = new();
    public List<HotkeyBinding> Hotkeys { get; set; } = [];
    public List<BlacklistRule> Blacklist { get; set; } = [];
}

public sealed class GeneralConfig
{
    public int TagCount { get; set; } = 9;
    public bool FocusFollowsMouse { get; set; } = false;
    public int BorderGap { get; set; } = 0;

    /// <summary>Layout every tag on every monitor starts on -- one of the names registered in
    /// LayoutRegistry (master-stack, master-stack-right, monocle, centered-master, vertical).
    /// Its nmaster/mfact/gap still come from the "master-stack" entry under layouts: below (every
    /// layout that uses those params reads the same one; per-tag/monitor layout config isn't
    /// supported yet). An unrecognized name is logged and that tag/monitor simply won't arrange
    /// until switched to a known layout -- same behavior as an invalid set-layout command.</summary>
    public string DefaultLayout { get; set; } = "master-stack";

    /// <summary>Strips the title bar and resize border (WS_CAPTION/WS_THICKFRAME) from every
    /// managed window -- applies globally, to tiled and floating windows alike. Original styles
    /// are remembered per-window and restored on exit, so turning this off (or quitting Wtile)
    /// gives windows their normal decorations back.</summary>
    public bool HideTitlebars { get; set; } = false;

    /// <summary>Width in px of the colored border drawn around whichever managed window currently
    /// has focus (dwm/mango-style). 0 disables it entirely.</summary>
    public int FocusedBorderWidth { get; set; } = 2;

    /// <summary>Color of the focused-window border, as "#RRGGBB". Ignored if FocusedBorderWidth is 0.</summary>
    public string FocusedBorderColor { get; set; } = "#89b4fa";

    /// <summary>Opt-in: on startup and on "reload", restores which monitor/tag each window was on
    /// last time (matched to newly-opened windows by process name + window class), and saves that
    /// placement to state.json on quit/reload. Off by default since it's new automatic-placement
    /// behavior a user hasn't asked for yet.</summary>
    public bool RememberLayout { get; set; } = false;
}

public sealed class LayoutConfig
{
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public int Nmaster { get; set; } = 1;
    public double Mfact { get; set; } = 0.55;
    public int Gap { get; set; } = 0;
}

public sealed class BarConfig
{
    public int Height { get; set; } = 24;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 10;
    public string Position { get; set; } = "top"; // "top" | "bottom"
    public BarSegmentsConfig Segments { get; set; } = new();
    public BarColorsConfig Colors { get; set; } = new();

    /// <summary>Optional per-module format-string overrides for the system-stat segments (cpu,
    /// memory, battery, network, volume) -- a module not listed here, or listed with an empty
    /// Format, uses its own built-in default.</summary>
    public List<BarModuleConfig> Modules { get; set; } = [];
}

public sealed class BarSegmentsConfig
{
    public List<string> Left { get; set; } = ["tags", "layout-symbol", "window-title"];
    public List<string> Right { get; set; } = ["clock"];
}

public sealed class BarModuleConfig
{
    public string Name { get; set; } = "";

    /// <summary>string.Format-style format string with positional placeholders ({0}, {1}, ...) --
    /// meaning is module-specific (see docs/config.sample.yaml). Empty (the default) means "use
    /// this module's built-in default format" -- same convention as BarColorsConfig.OccupiedTag.
    /// A malformed format string falls back to the built-in default at draw time rather than
    /// throwing (see Core/SegmentFormat.cs).</summary>
    public string Format { get; set; } = "";
}

public sealed class BarColorsConfig
{
    public string Background { get; set; } = "#1e1e2e";
    public string Foreground { get; set; } = "#cdd6f4";
    public string ActiveTag { get; set; } = "#89b4fa";
    public string UrgentTag { get; set; } = "#f38ba8";

    /// <summary>Background for an occupied-but-not-active tag pill. Empty (the default) means
    /// "derive one from background/foreground" -- set it explicitly to override, e.g. to
    /// <c>background</c> itself to make occupied tags visually blend in (the little occupancy
    /// square still shows either way).</summary>
    public string OccupiedTag { get; set; } = "";
}

public sealed class HotkeyBinding
{
    public string Keys { get; set; } = "";
    public string Command { get; set; } = "";
    public List<string> Args { get; set; } = [];
}

/// <summary>One window-exclusion rule: a window is blacklisted (never managed/tiled) if every
/// non-blank field here matches it as a regex (AND within a rule); the blacklist as a whole
/// excludes a window if any rule matches (OR across rules). A blank field means "don't constrain
/// on this field" -- a rule where every field is blank would match every window and is rejected
/// at load time (see ConfigLoader.Validate) rather than silently blacklisting everything.</summary>
public sealed class BlacklistRule
{
    public string ProcessName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Title { get; set; } = "";
}
