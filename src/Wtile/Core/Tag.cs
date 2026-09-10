namespace Wtile.Core;

/// <summary>
/// Per-tag layout state: each tag remembers its own layout selection and params (nmaster/
/// mfact/gap), matching dwm/bug.n -- adjusting mfact on tag 3 doesn't affect tag 1.
/// </summary>
internal sealed class Tag(string layoutName, IReadOnlyDictionary<string, double> layoutParams)
{
    public string LayoutName { get; set; } = layoutName;
    public Dictionary<string, double> LayoutParams { get; set; } = new(layoutParams);

    /// <summary>Last known non-zero gap, restored by toggle-gap; seeded from the initial/config
    /// gap so the very first toggle-on (before any manual adjustment) has something to restore.</summary>
    public double? RememberedGap { get; set; } = layoutParams.TryGetValue("gap", out double g) && g > 0 ? g : null;
}
