using Windows.Win32.Foundation;

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

    /// <summary>dwm's per-tag "sel": whichever window was last focused while this tag was the
    /// active one on its monitor. In-memory only, independent of general.rememberState -- see
    /// WindowManager.OnForegroundChanged (writer) and ActivateTag (reader, with a visible[0]
    /// fallback for a tag that's never had a focus of its own, or whose remembered window is gone
    /// or no longer visible there).</summary>
    public HWND LastFocusedHandle { get; set; }
}
