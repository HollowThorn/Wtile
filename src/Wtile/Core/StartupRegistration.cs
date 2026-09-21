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
/// changes so that's visible when it happens. Windows silently skips a Run entry whose exe has
/// since moved or been deleted, so a stale one is dead rather than harmful.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wtile";

    /// <summary>Reads as "off" if the registry can't be read for any reason -- a startup
    /// convenience must never take the window manager down over a tray click, same policy as
    /// SpawnCommand around Process.Start.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
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
                if (Environment.ProcessPath is not string exePath)
                {
                    Console.WriteLine("[launch-on-boot] Can't resolve this exe's path; not registered.");
                    return;
                }
                string command = $"\"{exePath}\"";
                if (key.GetValue(ValueName) as string != command)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.WriteLine($"[launch-on-boot] Failed to update the Run key: {ex.Message}");
        }
    }
}
