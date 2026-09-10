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

    /// <summary>Hides the real Windows taskbar as soon as Wtile starts, same as pressing
    /// toggle-taskbar (Win+Space by default) manually right after launch. Enforced once at
    /// startup regardless of whatever state the taskbar was left in by a previous run -- not
    /// reapplied on "reload", since that would fight the toggle-taskbar hotkey during a live
    /// session.</summary>
    public bool HideTaskbarOnStartup { get; set; } = false;
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
