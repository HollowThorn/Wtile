using System.Text.RegularExpressions;
using Wtile.Core;

namespace Wtile.Config;

/// <summary>Turns validated BlacklistRule/TagRule config entries (plain strings, YAML-shaped)
/// into the CompiledWindowRule/CompiledTagRule objects WindowManager actually matches against
/// (real Regex instances). Kept in Wtile.Config rather than Wtile.Core so Core stays free of any
/// Config dependency -- same direction as ConfigApplier, which already depends on Core, not the
/// reverse.</summary>
internal static class WindowRuleCompiler
{
    public static List<CompiledWindowRule> CompileBlacklist(IReadOnlyList<BlacklistRule> rules)
    {
        var compiled = new List<CompiledWindowRule>(rules.Count);
        foreach (BlacklistRule rule in rules)
            compiled.Add(Compile(rule.ProcessName, rule.ClassName, rule.Title));
        return compiled;
    }

    public static List<CompiledTagRule> CompileTagRules(IReadOnlyList<TagRule> rules)
    {
        var compiled = new List<CompiledTagRule>(rules.Count);
        foreach (TagRule rule in rules)
        {
            compiled.Add(new CompiledTagRule
            {
                Match = Compile(rule.ProcessName, rule.ClassName, rule.Title),
                TagIndex = rule.Tag - 1, // config is 1-based like the view-tag hotkeys; Core is 0-based
                MonitorIndex = rule.Monitor == 0 ? null : rule.Monitor - 1,
                Follow = rule.Follow,
            });
        }
        return compiled;
    }

    private static CompiledWindowRule Compile(string processName, string className, string title) =>
        new()
        {
            ProcessName = TryBuild(processName),
            ClassName = TryBuild(className),
            Title = TryBuild(title),
        };

    // ConfigLoader.Validate already guarantees every pattern reaching here is syntactically
    // valid, so this doesn't need its own try/catch.
    private static Regex? TryBuild(string pattern) =>
        string.IsNullOrWhiteSpace(pattern)
            ? null
            : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
