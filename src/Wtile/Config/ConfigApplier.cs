using Wtile.Bar;
using Wtile.Core;
using Wtile.Hotkeys;
using Wtile.Launcher;

namespace Wtile.Config;

/// <summary>Applies a (re)loaded config to the live WindowManager/BarWindows/HotkeyManager. Must
/// run on the main thread. Bar config (colors/font/segments) is shared config applied to every
/// monitor's bar identically -- matching the tag/layout config, which is also shared but then
/// diverges independently per monitor at runtime.</summary>
internal sealed class ConfigApplier(WindowManager manager, IReadOnlyList<BarWindow> bars, HotkeyManager hotkeys, FocusBorderWindow focusBorder, LauncherWindow launcher)
{
    public void Apply(WtileConfig config)
    {
        LayoutConfig? masterStack = config.Layouts.Find(l => l.Name == "master-stack");
        if (masterStack is not null)
        {
            manager.ResetAllTagLayoutParams(config.General.DefaultLayout, new Dictionary<string, double>
            {
                ["nmaster"] = masterStack.Nmaster,
                ["mfact"] = masterStack.Mfact,
                ["gap"] = masterStack.Gap,
            });
        }

        manager.UpdateTagCount(config.General.TagCount);
        if (manager.HideTitlebars != config.General.HideTitlebars)
            manager.SetHideTitlebars(config.General.HideTitlebars);
        manager.SetBlacklist(BlacklistCompiler.Compile(config.Blacklist));
        manager.SetRememberLayout(config.General.RememberLayout);
        focusBorder.ApplyConfig(config.General);
        foreach (BarWindow bar in bars)
            bar.ApplyConfig(config.Bar);
        launcher.ApplyConfig(config.Bar);
        hotkeys.ApplyBindings(config.Hotkeys);
    }
}
