using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Wtile.Bar;
using Wtile.Config;
using Wtile.Core;
using Wtile.Launcher;

namespace Wtile.Commands;

/// <summary>Switches the active tag. Args: <c>["&lt;1-based tag number&gt;"]</c>.</summary>
internal sealed class ViewTagCommand(WindowManager manager) : ICommand
{
    public string Name => "view-tag";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int tagNumber))
            return;
        manager.ActivateTag(tagNumber - 1);
    }
}

/// <summary>Moves the focused window to another tag. Args: <c>["&lt;1-based tag number&gt;"]</c>.</summary>
internal sealed class MoveWindowToTagCommand(WindowManager manager) : ICommand
{
    public string Name => "move-window-to-tag";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int tagNumber))
            return;
        manager.MoveWindowToTag(tagNumber - 1);
    }
}

internal sealed class FocusNextCommand(WindowManager manager) : ICommand
{
    public string Name => "focus-next";
    public void Execute(IReadOnlyList<string> args) => manager.FocusNext();
}

internal sealed class FocusPrevCommand(WindowManager manager) : ICommand
{
    public string Name => "focus-prev";
    public void Execute(IReadOnlyList<string> args) => manager.FocusPrev();
}

internal sealed class SwapMasterCommand(WindowManager manager) : ICommand
{
    public string Name => "swap-master";
    public void Execute(IReadOnlyList<string> args) => manager.SwapMaster();
}

internal sealed class ToggleFloatingCommand(WindowManager manager) : ICommand
{
    public string Name => "toggle-floating";
    public void Execute(IReadOnlyList<string> args) => manager.ToggleFloating();
}

/// <summary>bug.n's pin: the focused window stays visible/tiled on every tag, not just its own.</summary>
internal sealed class TogglePinnedCommand(WindowManager manager) : ICommand
{
    public string Name => "toggle-pinned";
    public void Execute(IReadOnlyList<string> args) => manager.TogglePinned();
}

/// <summary>dwm's view(~0): shows every tag's windows together. Switching to any specific tag exits it again.</summary>
internal sealed class ViewAllTagsCommand(WindowManager manager) : ICommand
{
    public string Name => "view-all-tags";
    public void Execute(IReadOnlyList<string> args) => manager.ViewAllTags();
}

/// <summary>Nudges master-stack's mfact. Args: <c>["&lt;signed delta, e.g. -0.05&gt;"]</c>.</summary>
internal sealed class AdjustMfactCommand(WindowManager manager) : ICommand
{
    public string Name => "adjust-mfact";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !double.TryParse(args[0], out double delta))
            return;
        manager.AdjustMfact(delta);
    }
}

/// <summary>Sends WM_CLOSE to the focused window -- same as clicking its close button.</summary>
internal sealed class KillWindowCommand(WindowManager manager) : ICommand
{
    public string Name => "kill-window";
    public void Execute(IReadOnlyList<string> args) => manager.CloseFocusedWindow();
}

/// <summary>Forcibly terminates the focused window's process -- for something kill-window's polite
/// WM_CLOSE doesn't budge (a hung app, or a system flyout/dialog that never processes WM_CLOSE at
/// all). Destructive: no save prompt, no chance for the app to object.</summary>
internal sealed class ForceKillWindowCommand(WindowManager manager) : ICommand
{
    public string Name => "force-kill-window";
    public void Execute(IReadOnlyList<string> args) => manager.ForceCloseFocusedWindow();
}

/// <summary>Nudges the active tag's master-area window count. Args: <c>["&lt;signed delta, e.g. +1&gt;"]</c>.</summary>
internal sealed class AdjustNmasterCommand(WindowManager manager) : ICommand
{
    public string Name => "adjust-nmaster";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !double.TryParse(args[0], out double delta))
            return;
        manager.AdjustNmaster(delta);
    }
}

/// <summary>Nudges the active tag's gap. Args: <c>["&lt;signed delta, e.g. -5&gt;"]</c>.</summary>
internal sealed class AdjustGapCommand(WindowManager manager) : ICommand
{
    public string Name => "adjust-gap";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !double.TryParse(args[0], out double delta))
            return;
        manager.AdjustGap(delta);
    }
}

/// <summary>dwm's togglegaps: zero the active tag's gap, or restore what it was before.</summary>
internal sealed class ToggleGapCommand(WindowManager manager) : ICommand
{
    public string Name => "toggle-gap";
    public void Execute(IReadOnlyList<string> args) => manager.ToggleGap();
}

/// <summary>Switches the active tag's layout. Args: <c>["&lt;layout name, e.g. master-stack-right&gt;"]</c>.</summary>
internal sealed class SetLayoutCommand(WindowManager manager) : ICommand
{
    public string Name => "set-layout";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return;
        manager.SetLayout(args[0]);
    }
}

/// <summary>Swaps the focused window with its stack neighbor. Args: <c>["&lt;+1 or -1&gt;"]</c>.</summary>
internal sealed class ShuffleWindowCommand(WindowManager manager) : ICommand
{
    public string Name => "shuffle-window";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int direction))
            return;
        manager.ShuffleWindow(direction);
    }
}

/// <summary>Views the tag N positions away, wrapping. Args: <c>["&lt;signed delta, e.g. -1&gt;"]</c>.</summary>
internal sealed class ShiftViewCommand(WindowManager manager) : ICommand
{
    public string Name => "shift-view";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int delta))
            return;
        manager.ShiftView(delta);
    }
}

/// <summary>Switches back to whichever tag was active before the current one.</summary>
internal sealed class ToggleLastTagCommand(WindowManager manager) : ICommand
{
    public string Name => "toggle-last-tag";
    public void Execute(IReadOnlyList<string> args) => manager.ToggleLastTag();
}

/// <summary>dwm's focusmon: switches which monitor hotkeys target. Args: <c>["&lt;+1 or -1&gt;"]</c>.</summary>
internal sealed class FocusMonitorCommand(WindowManager manager) : ICommand
{
    public string Name => "focus-monitor";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int delta))
            return;
        manager.FocusMonitor(delta);
    }
}

/// <summary>dwm's tagmon: moves the focused window to another monitor. Args: <c>["&lt;+1 or -1&gt;"]</c>.</summary>
internal sealed class MoveWindowToMonitorCommand(WindowManager manager) : ICommand
{
    public string Name => "move-window-to-monitor";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int delta))
            return;
        manager.MoveWindowToMonitor(delta);
    }
}

/// <summary>Args: <c>[exePath, ...processArgs]</c>. Never blocks the calling (main/UI) thread.</summary>
internal sealed class SpawnCommand : ICommand
{
    public string Name => "spawn";

    public void Execute(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return;

        var startInfo = new ProcessStartInfo(args[0]) { UseShellExecute = true };
        for (int i = 1; i < args.Count; i++)
            startInfo.ArgumentList.Add(args[i]);

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[spawn] Failed to start '{args[0]}': {ex.Message}");
        }
    }
}

/// <summary>Cleanly exits the message loop (Program.cs), letting Dispose/cleanup run.</summary>
internal sealed class QuitCommand : ICommand
{
    public string Name => "quit";
    public void Execute(IReadOnlyList<string> args) => PInvoke.PostQuitMessage(0);
}

/// <summary>Hides/shows the real Windows taskbar (not Windows' own auto-hide -- it never appears
/// at all) and reclaims/releases the space it occupies for tiling.</summary>
internal sealed class ToggleTaskbarCommand(WindowManager manager) : ICommand
{
    public string Name => "toggle-taskbar";
    public void Execute(IReadOnlyList<string> args) => manager.ToggleTaskbar();
}

/// <summary>Flips whether Wtile starts at Windows login (per-user Run key, see
/// StartupRegistration). What the tray menu's "Launch on boot" item runs; bindable as a hotkey
/// too. An explicit general.launchOnBoot puts it back on the next reload -- see ConfigModel.</summary>
internal sealed class ToggleLaunchOnBootCommand : ICommand
{
    public string Name => "toggle-launch-on-boot";
    public void Execute(IReadOnlyList<string> args) => StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled());
}

/// <summary>Prints the focused window's process/class/title to the console -- lets you copy exact
/// values straight into a blacklist: or tagRules: rule instead of guessing or reaching for Spy++.</summary>
internal sealed class InspectWindowCommand : ICommand
{
    public string Name => "inspect-window";

    public void Execute(IReadOnlyList<string> args)
    {
        HWND hwnd = PInvoke.GetForegroundWindow();
        WindowSnapshot snapshot = WindowInspector.Describe(hwnd);
        WindowInspector.TryGetProcessName(hwnd, out string processName);
        Console.WriteLine($"[inspect] process='{processName}' class='{snapshot.ClassName}' title='{snapshot.Title}'");
    }
}

/// <summary>
/// Re-reads config.yaml from disk and applies it (tags/colors/hotkeys/layout params/bar
/// segments), then unconditionally refreshes the bar's screen geometry and re-arranges --
/// covers both "I edited the config" and "I connected/disconnected a monitor" with one hotkey,
/// in place of a background file watcher for an event this rare. Registered separately in
/// Program.cs (not CreateDefault below) since it needs the BarWindow/ConfigApplier, which are
/// constructed after the initial CommandRegistry.
/// </summary>
internal sealed class ReloadCommand(string configPath, ConfigApplier applier, IReadOnlyList<BarWindow> bars, WindowManager manager, string statePath) : ICommand
{
    public string Name => "reload";

    public void Execute(IReadOnlyList<string> args)
    {
        // Save with the pre-reload RememberState value, before config (which may flip it) is
        // even read -- see WindowManager.RememberState / ApplySavedState.
        if (manager.RememberState)
            WindowStateStore.Save(statePath, manager.CaptureState());

        ConfigLoadResult result = ConfigLoader.LoadFromFile(configPath);
        foreach (string warning in result.Warnings)
            Console.WriteLine($"[config] warning: {warning}");
        applier.Apply(result.Config);
        foreach (BarWindow bar in bars)
            bar.RefreshGeometry();

        if (manager.RememberState && WindowStateStore.TryLoad(statePath, out SavedState state))
            manager.ApplySavedState(state);

        Console.WriteLine("[reload] Done.");
    }
}

/// <summary>Toggles the dmenu-style app launcher popup (PATH executables + Start Menu shortcuts,
/// fuzzy-filtered). Registered separately in Program.cs (not CreateDefault below), same reason as
/// ReloadCommand: needs LauncherWindow, which is constructed after the initial CommandRegistry.</summary>
internal sealed class AppLauncherCommand(LauncherWindow launcher) : ICommand
{
    public string Name => "app-launcher";
    public void Execute(IReadOnlyList<string> args) => launcher.Toggle();
}

/// <summary>Shows/hides Wtile's own bar on every monitor together (dwm/bug.n-style hiding of the
/// bar, distinct from toggle-taskbar which hides the real Windows taskbar) and reclaims/releases
/// the space it reserves for tiling. Registered separately in Program.cs, same reason as
/// ReloadCommand: needs the BarWindow list, constructed after the initial CommandRegistry.</summary>
internal sealed class ToggleBarCommand(IReadOnlyList<BarWindow> bars) : ICommand
{
    public string Name => "toggle-bar";

    public void Execute(IReadOnlyList<string> args)
    {
        bool showing = bars.Count == 0 || bars[0].IsVisible;
        foreach (BarWindow bar in bars)
            bar.SetVisible(!showing);
    }
}

internal static class BuiltinCommands
{
    public static CommandRegistry CreateDefault(WindowManager manager)
    {
        var registry = new CommandRegistry();
        registry.Register(new ViewTagCommand(manager));
        registry.Register(new MoveWindowToTagCommand(manager));
        registry.Register(new FocusNextCommand(manager));
        registry.Register(new FocusPrevCommand(manager));
        registry.Register(new SwapMasterCommand(manager));
        registry.Register(new ToggleFloatingCommand(manager));
        registry.Register(new TogglePinnedCommand(manager));
        registry.Register(new ViewAllTagsCommand(manager));
        registry.Register(new AdjustMfactCommand(manager));
        registry.Register(new AdjustNmasterCommand(manager));
        registry.Register(new AdjustGapCommand(manager));
        registry.Register(new ToggleGapCommand(manager));
        registry.Register(new SetLayoutCommand(manager));
        registry.Register(new ShuffleWindowCommand(manager));
        registry.Register(new ShiftViewCommand(manager));
        registry.Register(new ToggleLastTagCommand(manager));
        registry.Register(new FocusMonitorCommand(manager));
        registry.Register(new MoveWindowToMonitorCommand(manager));
        registry.Register(new KillWindowCommand(manager));
        registry.Register(new ForceKillWindowCommand(manager));
        registry.Register(new SpawnCommand());
        registry.Register(new QuitCommand());
        registry.Register(new ToggleTaskbarCommand(manager));
        registry.Register(new ToggleLaunchOnBootCommand());
        registry.Register(new InspectWindowCommand());
        return registry;
    }
}
