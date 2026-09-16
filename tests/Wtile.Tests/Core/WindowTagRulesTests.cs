using System.Text.RegularExpressions;
using Wtile.Core;

namespace Wtile.Tests.Core;

public class WindowTagRulesTests
{
    private static CompiledTagRule Rule(int tagIndex, string? processName = null, string? className = null, string? title = null, bool follow = false) =>
        new()
        {
            Match = new CompiledWindowRule
            {
                ProcessName = processName is null ? null : new Regex(processName, RegexOptions.IgnoreCase),
                ClassName = className is null ? null : new Regex(className, RegexOptions.IgnoreCase),
                Title = title is null ? null : new Regex(title, RegexOptions.IgnoreCase),
            },
            TagIndex = tagIndex,
            Follow = follow,
        };

    [Fact]
    public void NoRules_ResolvesNothing()
    {
        Assert.False(WindowTagRules.TryResolve([], "firefox.exe", "MozillaWindowClass", "Mozilla Firefox", out _));
    }

    [Fact]
    public void SingleFieldMatch_ResolvesToThatRule()
    {
        var rules = new List<CompiledTagRule> { Rule(1, processName: "^firefox\\.exe$", follow: true) };

        Assert.True(WindowTagRules.TryResolve(rules, "firefox.exe", "MozillaWindowClass", "Mozilla Firefox", out CompiledTagRule matched));
        Assert.Equal(1, matched.TagIndex);
        Assert.True(matched.Follow);
    }

    [Fact]
    public void SingleFieldNonMatch_ResolvesNothing()
    {
        var rules = new List<CompiledTagRule> { Rule(1, processName: "^firefox\\.exe$") };
        Assert.False(WindowTagRules.TryResolve(rules, "chrome.exe", "Chrome_WidgetWin_1", "My Tab", out _));
    }

    [Fact]
    public void MultiFieldRule_RequiresAllSetFieldsToMatch()
    {
        var rules = new List<CompiledTagRule> { Rule(4, processName: "chrome", title: "Netflix") };

        Assert.False(WindowTagRules.TryResolve(rules, "chrome.exe", "Chrome_WidgetWin_1", "Gmail", out _));
        Assert.True(WindowTagRules.TryResolve(rules, "chrome.exe", "Chrome_WidgetWin_1", "Netflix - Chrome", out CompiledTagRule matched));
        Assert.Equal(4, matched.TagIndex);
    }

    [Fact]
    public void MultipleMatchingRules_FirstWins()
    {
        var rules = new List<CompiledTagRule> { Rule(2, className: "Foo"), Rule(5, processName: "any") };

        Assert.True(WindowTagRules.TryResolve(rules, "any.exe", "Foo", "", out CompiledTagRule matched));
        Assert.Equal(2, matched.TagIndex);
    }

    [Fact]
    public void BlankRuleField_ActsAsWildcard()
    {
        var rules = new List<CompiledTagRule> { Rule(0, className: "AnyClass") };
        Assert.True(WindowTagRules.TryResolve(rules, "whatever.exe", "AnyClass", "any title at all", out _));
    }
}
