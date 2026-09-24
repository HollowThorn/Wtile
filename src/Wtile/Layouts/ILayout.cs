namespace Wtile.Layouts;

/// <summary>A window rectangle in physical screen pixels, relative to the virtual desktop origin.</summary>
public readonly record struct LayoutRect(int X, int Y, int Width, int Height)
{
    /// <summary>Degenerate: nothing can be laid out in it. Notably what WindowInspector returns
    /// for a monitor handle GetMonitorInfo rejects (a stale HMONITOR after the display config
    /// changed), so callers use it to tell "no usable geometry" from a real rect instead of
    /// tiling everything into a 0x0 box at the screen origin.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Inputs to a layout's arrangement pass. Deliberately holds counts/params rather than
/// <c>ManagedWindow</c> objects so layouts stay pure functions of geometry, independently
/// unit-testable without any Win32 state.
/// </summary>
public readonly record struct LayoutContext(
    LayoutRect WorkArea,
    int TiledWindowCount,
    IReadOnlyDictionary<string, double> Params);

/// <summary>
/// A pluggable tiling algorithm, registered by <see cref="Name"/> and referenced from YAML config
/// (e.g. <c>layout: master-stack</c>). Implementations must be pure: same inputs, same outputs,
/// no Win32 calls, no mutable state carried between calls.
/// </summary>
public interface ILayout
{
    /// <summary>Registry key, referenced by name from YAML config.</summary>
    string Name { get; }

    /// <summary>Short glyph shown in the bar's layout segment (dwm-style, e.g. "[]=").</summary>
    string Symbol { get; }

    /// <summary>
    /// Computes one rectangle per tiled window. The returned list has exactly
    /// <see cref="LayoutContext.TiledWindowCount"/> entries, in stack order (index 0 = master).
    /// </summary>
    IReadOnlyList<LayoutRect> Arrange(in LayoutContext context);
}
