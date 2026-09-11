using System.Text.RegularExpressions;
using Wtile.Hotkeys;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wtile.Config;

public sealed class ConfigLoadResult
{
    public required WtileConfig Config { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Parses YAML config text into a validated <see cref="WtileConfig"/>, applying defaults and
/// clamping out-of-range values rather than throwing -- a hand-edited hot-reloaded config file
/// should degrade gracefully, not crash the running window manager.
/// </summary>
public static class ConfigLoader
{
    public static ConfigLoadResult LoadFromFile(string path) => Load(File.ReadAllText(path));

    public static ConfigLoadResult Load(string yaml)
    {
        var warnings = new List<string>();

        IDeserializer deserializer = new StaticDeserializerBuilder(new WtileYamlContext())
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        WtileConfig config = string.IsNullOrWhiteSpace(yaml)
            ? new WtileConfig()
            : deserializer.Deserialize<WtileConfig>(yaml) ?? new WtileConfig();

        Validate(config, warnings);

        return new ConfigLoadResult { Config = config, Warnings = warnings };
    }

    private static void Validate(WtileConfig config, List<string> warnings)
    {
        if (config.General.TagCount < 1)
        {
            warnings.Add($"general.tagCount must be >= 1 (was {config.General.TagCount}); defaulting to 9.");
            config.General.TagCount = 9;
        }
        if (config.General.BorderGap < 0)
        {
            warnings.Add("general.borderGap cannot be negative; clamped to 0.");
            config.General.BorderGap = 0;
        }
        if (config.General.FocusedBorderWidth < 0)
        {
            warnings.Add("general.focusedBorderWidth cannot be negative; clamped to 0.");
            config.General.FocusedBorderWidth = 0;
        }

        if (config.Layouts.Count == 0)
        {
            config.Layouts.Add(new LayoutConfig
            {
                Name = "master-stack",
                Symbol = "[]=",
                Nmaster = 1,
                Mfact = 0.55,
                Gap = config.General.BorderGap,
            });
        }

        foreach (LayoutConfig layout in config.Layouts)
        {
            if (string.IsNullOrWhiteSpace(layout.Name))
                warnings.Add("A layout entry is missing a name and will be unreachable from hotkeys/tags.");

            if (layout.Mfact is < 0.05 or > 0.95)
            {
                warnings.Add($"layouts[{layout.Name}].mfact must be between 0.05 and 0.95 (was {layout.Mfact}); clamped.");
                layout.Mfact = Math.Clamp(layout.Mfact, 0.05, 0.95);
            }
            if (layout.Nmaster < 0)
            {
                warnings.Add($"layouts[{layout.Name}].nmaster cannot be negative; clamped to 0.");
                layout.Nmaster = 0;
            }
            if (layout.Gap < 0)
            {
                warnings.Add($"layouts[{layout.Name}].gap cannot be negative; clamped to 0.");
                layout.Gap = 0;
            }
        }

        if (config.Bar.Position is not ("top" or "bottom"))
        {
            warnings.Add($"bar.position must be 'top' or 'bottom' (was '{config.Bar.Position}'); defaulting to 'top'.");
            config.Bar.Position = "top";
        }
        if (config.Bar.Height <= 0)
        {
            warnings.Add($"bar.height must be positive (was {config.Bar.Height}); defaulting to 24.");
            config.Bar.Height = 24;
        }

        var validHotkeys = new List<HotkeyBinding>(config.Hotkeys.Count);
        foreach (HotkeyBinding binding in config.Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(binding.Command))
            {
                warnings.Add($"hotkeys entry '{binding.Keys}' has no command and will be ignored.");
                continue;
            }
            if (!KeyComboParser.TryParse(binding.Keys, out _))
            {
                warnings.Add($"hotkeys entry has an unparseable key combo '{binding.Keys}' and will be ignored.");
                continue;
            }
            validHotkeys.Add(binding);
        }
        config.Hotkeys = validHotkeys;

        var validRules = new List<BlacklistRule>(config.Blacklist.Count);
        foreach (BlacklistRule rule in config.Blacklist)
        {
            if (string.IsNullOrWhiteSpace(rule.ProcessName) && string.IsNullOrWhiteSpace(rule.ClassName) && string.IsNullOrWhiteSpace(rule.Title))
            {
                warnings.Add("A blacklist entry has no processName/className/title set and would match every window; ignored.");
                continue;
            }
            if (IsValidPattern(rule.ProcessName, "processName", warnings)
                && IsValidPattern(rule.ClassName, "className", warnings)
                && IsValidPattern(rule.Title, "title", warnings))
            {
                validRules.Add(rule);
            }
        }
        config.Blacklist = validRules;
    }

    private static bool IsValidPattern(string pattern, string fieldName, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;
        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            warnings.Add($"blacklist entry has an invalid {fieldName} regex '{pattern}' and will be ignored.");
            return false;
        }
    }
}
