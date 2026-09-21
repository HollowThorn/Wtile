using Microsoft.Win32;

namespace Wtile.Core;

/// <summary>
/// Registers/unregisters Wtile to start at Windows login through the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run) -- the same mechanism behind Steam's or
/// Discord's "run at startup" checkbox. Per-user, so it never needs elevation, and it's one string
/// value rather than a Startup-folder .lnk (which would need IShellLink COM plumbing for no gain).
/// Microsoft.Win32.Registry is used directly instead of CsWin32's RegSetValueEx since it's inbox
/// on the windows TFM, AOT-safe, and already does the handle/error dance.
///
/// The registered command is whichever exe is currently running (Environment.ProcessPath), so
/// registering from a debug build in bin\ registers *that* build -- the path is logged whenever it
/// changes so that's visible when it happens. "Enabled" therefore means "*this* exe is the one
/// registered", not "some Wtile is": with several builds around, each sharing the one value,
/// another build's registration reads as off here, and ticking from this build overwrites it --
/// the last build ticked from wins. Same for a stale entry whose exe has since moved (Windows
/// silently skips those): reads as off, ticking on rewrites it.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wtile";

    /// <summary>The value this exe registers under ValueName (quoted, since the install path has
    /// spaces in it by default), and the one IsEnabled compares against. Null if the runtime
    /// can't tell where this exe lives.</summary>
    private static string? CurrentCommand =>
        Environment.ProcessPath is string exePath ? $"\"{exePath}\"" : null;

    private static bool IsCurrentCommand(object? registered) =>
        registered is string value && CurrentCommand is string command
        && string.Equals(value, command, StringComparison.OrdinalIgnoreCase); // NTFS paths are case-insensitive

    /// <summary>Reads as "off" if the registry can't be read for any reason -- a startup
    /// convenience must never take the window manager down over a tray click, same policy as
    /// SpawnCommand around Process.Start.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return IsCurrentCommand(key?.GetValue(ValueName));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[launch-on-boot] Failed to read the Run key: {ex.Message}");
            return false;
        }
    }

    /// <summary>general.launchOnBoot: omitted (null) leaves the Run entry alone, making the tray
    /// menu's toggle the only control; true/false is enforced on every startup and reload.</summary>
    public static void ApplyConfig(bool? launchOnBoot)
    {
        if (launchOnBoot is bool enabled)
            SetEnabled(enabled);
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                if (CurrentCommand is not string command)
                {
                    Console.WriteLine("[launch-on-boot] Can't resolve this exe's path; not registered.");
                    return;
                }
                if (!IsCurrentCommand(key.GetValue(ValueName)))
                {
                    key.SetValue(ValueName, command);
                    Console.WriteLine($"[launch-on-boot] Registered {command}");
                }
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
                Console.WriteLine("[launch-on-boot] Unregistered.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[launch-on-boot] Failed to update the Run key: {ex.Message}");
        }
    }
}
