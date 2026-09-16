namespace Wtile.Core;

/// <summary>A compiled tagRules: entry: which tag (0-based here -- ConfigLoader validated the
/// user's 1-based number and WindowRuleCompiler converted it) a window matching
/// <see cref="Match"/> is dropped onto when first seen, optionally which monitor (0-based, null
/// = the one it opened on; not range-checked until use since the monitor count isn't known at
/// config time), and whether the view should switch to that tag as it appears (dwm's
/// switchtotag patch, per rule) or stay where it is.</summary>
public sealed class CompiledTagRule
{
    public required CompiledWindowRule Match { get; init; }
    public required int TagIndex { get; init; }
    public int? MonitorIndex { get; init; }
    public bool Follow { get; init; }
}

/// <summary>Decides which tag a newly-seen window belongs on, per the user-configured tag rules:
/// first matching rule wins (a Wtile window has exactly one tag, so there's nothing to OR
/// together the way dwm's rules OR their tag masks). Like WindowBlacklist, checked once per
/// window in WindowManager.TryAdd, never in the arrange/layout hot path.</summary>
public static class WindowTagRules
{
    public static bool TryResolve(IReadOnlyList<CompiledTagRule> rules, string processName, string className, string title, out CompiledTagRule matched)
    {
        foreach (CompiledTagRule rule in rules)
        {
            if (rule.Match.Matches(processName, className, title))
            {
                matched = rule;
                return true;
            }
        }
        matched = null!;
        return false;
    }
}
