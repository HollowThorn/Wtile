using Wtile.Config;

namespace Wtile.Tests.Config;

public class ConfigLoaderTests
{
    private static string SampleConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "config.sample.yaml");

    [Fact]
    public void EmptyYaml_ProducesDefaultsWithNoWarnings()
    {
        ConfigLoadResult result = ConfigLoader.Load("");

        Assert.Empty(result.Warnings);
        Assert.Equal(9, result.Config.General.TagCount);
        Assert.Single(result.Config.Layouts);
        Assert.Equal("master-stack", result.Config.Layouts[0].Name);
        Assert.Equal("top", result.Config.Bar.Position);
    }

    [Fact]
    public void ParsesGeneralSection()
    {
        const string yaml = """
            general:
              tagCount: 5
              focusFollowsMouse: true
              borderGap: 8
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Equal(5, result.Config.General.TagCount);
        Assert.True(result.Config.General.FocusFollowsMouse);
        Assert.Equal(8, result.Config.General.BorderGap);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParsesLayoutsAndBarAndHotkeys()
    {
        const string yaml = """
            layouts:
              - name: master-stack
                symbol: "[]="
                nmaster: 2
                mfact: 0.6
                gap: 4

            bar:
              height: 30
              position: bottom
              segments:
                left: [tags, window-title]
                right: [clock]

            hotkeys:
              - keys: "Alt+J"
                command: focus-next
              - keys: "Alt+1"
                command: view-tag
                args: ["1"]
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Warnings);

        LayoutConfig layout = Assert.Single(result.Config.Layouts);
        Assert.Equal(2, layout.Nmaster);
        Assert.Equal(0.6, layout.Mfact);
        Assert.Equal(4, layout.Gap);

        Assert.Equal(30, result.Config.Bar.Height);
        Assert.Equal("bottom", result.Config.Bar.Position);
        Assert.Equal(["tags", "window-title"], result.Config.Bar.Segments.Left);

        Assert.Equal(2, result.Config.Hotkeys.Count);
        Assert.Equal("focus-next", result.Config.Hotkeys[0].Command);
        Assert.Equal(["1"], result.Config.Hotkeys[1].Args);
    }

    [Fact]
    public void OutOfRangeMfact_IsClampedWithWarning()
    {
        const string yaml = """
            layouts:
              - name: master-stack
                mfact: 1.5
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Equal(0.95, result.Config.Layouts[0].Mfact);
        Assert.Contains(result.Warnings, w => w.Contains("mfact"));
    }

    [Fact]
    public void NegativeTagCount_DefaultsToNineWithWarning()
    {
        const string yaml = "general:\n  tagCount: -3\n";

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Equal(9, result.Config.General.TagCount);
        Assert.Contains(result.Warnings, w => w.Contains("tagCount"));
    }

    [Fact]
    public void NegativeFocusedBorderWidth_ClampedToZeroWithWarning()
    {
        const string yaml = "general:\n  focusedBorderWidth: -2\n";

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Equal(0, result.Config.General.FocusedBorderWidth);
        Assert.Contains(result.Warnings, w => w.Contains("focusedBorderWidth"));
    }

    [Fact]
    public void InvalidBarPosition_DefaultsToTopWithWarning()
    {
        const string yaml = "bar:\n  position: sideways\n";

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Equal("top", result.Config.Bar.Position);
        Assert.Contains(result.Warnings, w => w.Contains("position"));
    }

    [Fact]
    public void UnparseableHotkey_IsDroppedWithWarning()
    {
        const string yaml = """
            hotkeys:
              - keys: "NotAValidCombo"
                command: focus-next
              - keys: "Alt+J"
                command: focus-next
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Single(result.Config.Hotkeys);
        Assert.Equal("Alt+J", result.Config.Hotkeys[0].Keys);
        Assert.Contains(result.Warnings, w => w.Contains("NotAValidCombo"));
    }

    [Fact]
    public void HotkeyWithoutCommand_IsDroppedWithWarning()
    {
        const string yaml = """
            hotkeys:
              - keys: "Alt+J"
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.Hotkeys);
        Assert.Contains(result.Warnings, w => w.Contains("no command"));
    }

    [Fact]
    public void ParsesBlacklistSection()
    {
        const string yaml = """
            blacklist:
              - processName: "^steam\\.exe$"
                title: "Friends"
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Warnings);
        BlacklistRule rule = Assert.Single(result.Config.Blacklist);
        Assert.Equal("^steam\\.exe$", rule.ProcessName);
        Assert.Equal("Friends", rule.Title);
    }

    [Fact]
    public void InvalidBlacklistRegex_IsDroppedWithWarning()
    {
        const string yaml = """
            blacklist:
              - className: "("
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.Blacklist);
        Assert.Contains(result.Warnings, w => w.Contains("className"));
    }

    [Fact]
    public void AllBlankBlacklistRule_IsDroppedWithWarning()
    {
        const string yaml = """
            blacklist:
              - processName: ""
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.Blacklist);
        Assert.Contains(result.Warnings, w => w.Contains("would match every window"));
    }

    [Fact]
    public void ParsesTagRulesSection()
    {
        const string yaml = """
            tagRules:
              - processName: "^firefox\\.exe$"
                tag: 2
                follow: true
              - className: "CASCADIA_HOSTING_WINDOW_CLASS"
                tag: 3
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Config.TagRules.Count);
        Assert.Equal("^firefox\\.exe$", result.Config.TagRules[0].ProcessName);
        Assert.Equal(2, result.Config.TagRules[0].Tag);
        Assert.True(result.Config.TagRules[0].Follow);
        Assert.Equal("CASCADIA_HOSTING_WINDOW_CLASS", result.Config.TagRules[1].ClassName);
        Assert.Equal(3, result.Config.TagRules[1].Tag);
        Assert.False(result.Config.TagRules[1].Follow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void TagRuleOutsideTagCount_IsDroppedWithWarning(int tag)
    {
        string yaml = $"""
            general:
              tagCount: 5
            tagRules:
              - processName: "firefox"
                tag: {tag}
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.TagRules);
        Assert.Contains(result.Warnings, w => w.Contains("outside 1..5"));
    }

    [Fact]
    public void TagRuleMissingTag_IsDroppedWithWarning()
    {
        const string yaml = """
            tagRules:
              - processName: "firefox"
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.TagRules);
        Assert.Contains(result.Warnings, w => w.Contains("tag 0"));
    }

    [Fact]
    public void AllBlankTagRule_IsDroppedWithWarning()
    {
        const string yaml = """
            tagRules:
              - tag: 2
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.TagRules);
        Assert.Contains(result.Warnings, w => w.Contains("would match every window"));
    }

    [Fact]
    public void InvalidTagRuleRegex_IsDroppedWithWarning()
    {
        const string yaml = """
            tagRules:
              - title: "("
                tag: 1
            """;

        ConfigLoadResult result = ConfigLoader.Load(yaml);

        Assert.Empty(result.Config.TagRules);
        Assert.Contains(result.Warnings, w => w.Contains("tagRules") && w.Contains("title"));
    }

    [Fact]
    public void RealSampleConfig_ParsesCleanlyWithNoWarnings()
    {
        ConfigLoadResult result = ConfigLoader.LoadFromFile(SampleConfigPath);

        Assert.Empty(result.Warnings);
        Assert.Equal(9, result.Config.General.TagCount);
        Assert.NotEmpty(result.Config.Hotkeys);
    }
}
