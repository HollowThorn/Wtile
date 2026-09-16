namespace Wtile.Core;

/// <summary>Decides whether a window should be excluded from management entirely, per the
/// user-configured blacklist: rules match, if any rule matches -- checked once, when a window is
/// first seen (WindowManager.TryAdd), never in the arrange/layout hot path.</summary>
public static class WindowBlacklist
{
    public static bool IsBlacklisted(IReadOnlyList<CompiledWindowRule> rules, string processName, string className, string title)
    {
        foreach (CompiledWindowRule rule in rules)
        {
            if (rule.Matches(processName, className, title))
                return true;
        }
        return false;
    }
}
