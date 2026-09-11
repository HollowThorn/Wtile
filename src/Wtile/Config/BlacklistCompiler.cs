using System.Text.RegularExpressions;
using Wtile.Core;

namespace Wtile.Config;

/// <summary>Turns validated BlacklistRule config entries (plain strings, YAML-shaped) into the
/// CompiledBlacklistRule objects WindowManager actually matches against (real Regex instances).
/// Kept in Wtile.Config rather than Wtile.Core so Core stays free of any Config dependency --
/// same direction as ConfigApplier, which already depends on Core, not the reverse.</summary>
internal static class BlacklistCompiler
{
    public static List<CompiledBlacklistRule> Compile(IReadOnlyList<BlacklistRule> rules)
    {
        var compiled = new List<CompiledBlacklistRule>(rules.Count);
        foreach (BlacklistRule rule in rules)
        {
            compiled.Add(new CompiledBlacklistRule
            {
                ProcessName = TryBuild(rule.ProcessName),
                ClassName = TryBuild(rule.ClassName),
                Title = TryBuild(rule.Title),
            });
        }
        return compiled;
    }

    // ConfigLoader.Validate already guarantees every pattern reaching here is syntactically
    // valid, so this doesn't need its own try/catch.
    private static Regex? TryBuild(string pattern) =>
        string.IsNullOrWhiteSpace(pattern)
            ? null
            : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
