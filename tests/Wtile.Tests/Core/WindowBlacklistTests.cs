using System.Text.RegularExpressions;
using Wtile.Core;

namespace Wtile.Tests.Core;

public class WindowBlacklistTests
{
    private static CompiledBlacklistRule Rule(string? processName = null, string? className = null, string? title = null) =>
        new()
        {
            ProcessName = processName is null ? null : new Regex(processName, RegexOptions.IgnoreCase),
            ClassName = className is null ? null : new Regex(className, RegexOptions.IgnoreCase),
            Title = title is null ? null : new Regex(title, RegexOptions.IgnoreCase),
        };

    [Fact]
    public void NoRules_NothingIsBlacklisted()
    {
        Assert.False(WindowBlacklist.IsBlacklisted([], "chrome.exe", "Chrome_WidgetWin_1", "My Tab"));
    }

    [Fact]
    public void SingleFieldMatch_IsBlacklisted()
    {
        var rules = new List<CompiledBlacklistRule> { Rule(className: "NativeHWNDHost") };
        Assert.True(WindowBlacklist.IsBlacklisted(rules, "sihost.exe", "NativeHWNDHost", ""));
    }

    [Fact]
    public void SingleFieldNonMatch_IsNotBlacklisted()
    {
        var rules = new List<CompiledBlacklistRule> { Rule(className: "NativeHWNDHost") };
        Assert.False(WindowBlacklist.IsBlacklisted(rules, "chrome.exe", "Chrome_WidgetWin_1", "My Tab"));
    }

    [Fact]
    public void MultiFieldRule_RequiresAllSetFieldsToMatch()
    {
        var rules = new List<CompiledBlacklistRule> { Rule(processName: "chrome", title: "Netflix") };

        Assert.False(WindowBlacklist.IsBlacklisted(rules, "chrome.exe", "Chrome_WidgetWin_1", "Gmail"));
        Assert.True(WindowBlacklist.IsBlacklisted(rules, "chrome.exe", "Chrome_WidgetWin_1", "Netflix - Chrome"));
    }

    [Fact]
    public void MultipleRules_MatchesIfAnyRuleMatches()
    {
        var rules = new List<CompiledBlacklistRule> { Rule(className: "Foo"), Rule(className: "Bar") };
        Assert.True(WindowBlacklist.IsBlacklisted(rules, "", "Bar", ""));
    }

    [Fact]
    public void BlankRuleField_ActsAsWildcard()
    {
        var rules = new List<CompiledBlacklistRule> { Rule(className: "AnyClass") };
        Assert.True(WindowBlacklist.IsBlacklisted(rules, "whatever.exe", "AnyClass", "any title at all"));
    }
}
