namespace Wtile.Core;

/// <summary>
/// Everything ApplySavedState's matcher needs about one currently-tracked live window, captured
/// as plain data so the matching algorithm stays a pure function -- independently testable
/// without any real HWND/Win32 state. Handle is the same opaque raw value SavedWindowState
/// persists (see WindowManager.CaptureState/ApplySavedState for the HWND&lt;-&gt;long conversion).
/// </summary>
public readonly record struct LiveWindowSnapshot(long Handle, string ProcessName, string ClassName, string Title);

/// <summary>
/// Matches saved window records (see WindowState.cs) to currently-live windows, since HWNDs
/// aren't reliably stable across a restart. Three-tier precedence, each tier run as a full pass
/// over every saved record not already resolved by an earlier tier, against every live window not
/// already claimed by an earlier tier -- a full pass per tier (rather than resolving each saved
/// record greedily top to bottom) so a low-confidence tier-C match can never claim a live window
/// out from under a later saved record's tier-A exact-handle match:
///   A. Exact handle match (handle + process + class all agree) -- provably correct right after
///      an in-process reload (ReloadCommand), where HWNDs cannot have changed; still safe after a
///      real restart since it's gated on process+class also agreeing (guards against a recycled
///      handle landing on an unrelated window).
///   B. Title tie-break -- among remaining process+class candidates, prefer one whose current
///      title exactly equals the saved title (e.g. two Chrome windows on different tabs/pages).
///   C. Process+class FIFO -- the original heuristic, used whenever neither signal above resolves
///      it (e.g. both candidates have the exact same title, or titles churned since save).
/// A saved record with an empty ProcessName never matches (avoids false positives between two
/// access-denied windows).
/// </summary>
public static class WindowStateMatcher
{
    /// <summary>Returns, for each index into <paramref name="saved"/> that found a match, the
    /// Handle of the live window it matched. Indices with no match are simply absent.</summary>
    public static Dictionary<int, long> Match(IReadOnlyList<SavedWindowState> saved, IReadOnlyList<LiveWindowSnapshot> live)
    {
        var result = new Dictionary<int, long>();
        var claimedLive = new HashSet<long>();

        MatchTier(saved, live, result, claimedLive, requireHandle: true, requireTitle: false);
        MatchTier(saved, live, result, claimedLive, requireHandle: false, requireTitle: true);
        MatchTier(saved, live, result, claimedLive, requireHandle: false, requireTitle: false);

        return result;
    }

    private static void MatchTier(
        IReadOnlyList<SavedWindowState> saved, IReadOnlyList<LiveWindowSnapshot> live,
        Dictionary<int, long> result, HashSet<long> claimedLive,
        bool requireHandle, bool requireTitle)
    {
        for (int i = 0; i < saved.Count; i++)
        {
            if (result.ContainsKey(i))
                continue;

            SavedWindowState s = saved[i];
            if (string.IsNullOrEmpty(s.ProcessName))
                continue; // never matches -- avoids false positives between two access-denied windows
            if (requireHandle && s.Handle == 0)
                continue;
            if (requireTitle && string.IsNullOrEmpty(s.Title))
                continue;

            foreach (LiveWindowSnapshot w in live)
            {
                if (claimedLive.Contains(w.Handle))
                    continue;
                if (w.ClassName != s.ClassName || w.ProcessName != s.ProcessName)
                    continue;
                if (requireHandle && w.Handle != s.Handle)
                    continue;
                if (requireTitle && w.Title != s.Title)
                    continue;

                result[i] = w.Handle;
                claimedLive.Add(w.Handle);
                break;
            }
        }
    }
}
