using System.Text.RegularExpressions;

namespace Wtile.Core;

/// <summary>A compiled window-matching rule, shared by the blacklist (see WindowBlacklist) and
/// tag rules (see WindowTagRules): <c>null</c> on a field means "don't constrain on this field"
/// (wildcard); every non-null field must match for the rule as a whole to match (AND). Regexes
/// are interpreted (never RegexOptions.Compiled -- that uses Reflection.Emit, which is not
/// NativeAOT-safe) and carry a short match timeout so a pathological user pattern can't hang the
/// single thread that drives the whole WM (see WindowManager's class doc comment).</summary>
public sealed class CompiledWindowRule
{
    public Regex? ProcessName { get; init; }
    public Regex? ClassName { get; init; }
    public Regex? Title { get; init; }

    public bool Matches(string processName, string className, string title) =>
        (ProcessName is null || SafeIsMatch(ProcessName, processName))
        && (ClassName is null || SafeIsMatch(ClassName, className))
        && (Title is null || SafeIsMatch(Title, title));

    private static bool SafeIsMatch(Regex regex, string input)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false; // fail open rather than hang the WinEventHook pump thread
        }
    }
}
