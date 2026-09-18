namespace Wtile.Core;

/// <summary>
/// The autostart: entries whose window hasn't shown up yet, by process name. WindowManager.TryAdd
/// asks it about every newly managed window while anything is pending; the first window from a
/// pending entry's exe consumes that entry and is placed without following, so a follow: true
/// tagRule takes you to the app when *you* open it, not while the desktop is still assembling
/// itself at login. Once the list empties the check costs nothing (no per-window process-name
/// syscall either, see TryAdd). An entry whose app never opens a window -- crashed, background
/// only, or a packaged app whose real exe name wasn't given via processName: -- just stays
/// pending, harmlessly.
/// </summary>
public sealed class AutostartTracker
{
    private readonly List<string> _pending;

    public AutostartTracker(IEnumerable<string> processNames) => _pending = [.. processNames];

    public int PendingCount => _pending.Count;

    /// <summary>Case-insensitive (Win32 file names are), consuming exactly one entry per window
    /// so two autostart entries for the same exe absorb two windows, not one.</summary>
    public bool TryConsume(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return false;
        int index = _pending.FindIndex(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;
        _pending.RemoveAt(index);
        return true;
    }
}
