using Microsoft.Win32;

namespace Wtile.Core;

// launchOnBoot: user. The per-user Run key needs no elevation; Microsoft.Win32.Registry is inbox on
// the windows TFM and AOT-safe. It registers whichever exe is running, so with several builds
// around the last one to apply the config wins.
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wtile";

    private static string? CurrentCommand =>
        Environment.ProcessPath is string exePath ? $"\"{exePath}\"" : null;

    private static bool IsCurrentCommand(object? registered) =>
        registered is string value && CurrentCommand is string command
        && string.Equals(value, command, StringComparison.OrdinalIgnoreCase); // NTFS paths are case-insensitive

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
