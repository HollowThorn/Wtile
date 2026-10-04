namespace Wtile.Core;

// general.launchOnBoot. Only one mechanism may be active at a time: both would start two Wtiles at
// login, and only one survives the single-instance mutex.
internal static class LaunchOnBoot
{
    public const string Off = "off";
    public const string User = "user";
    public const string Admin = "admin";

    private static readonly object ApplyLock = new();
    private static volatile string? _requestedMode;

    public static bool IsValidMode(string mode) => mode is Off or User or Admin;

    // Applying runs schtasks and may wait on a UAC prompt for as long as it stays open, so it
    // must never run on the thread that owns the keyboard hook and every window.
    public static void ApplyInBackground(string mode)
    {
        if (mode == _requestedMode)
            return;
        _requestedMode = mode;

        var worker = new Thread(() => ApplyIfStillRequested(mode)) { IsBackground = true, Name = "launch-on-boot" };
        worker.SetApartmentState(ApartmentState.STA); // ShellExecuteEx, which shows the UAC prompt, needs COM
        worker.Start();
    }

    private static void ApplyIfStillRequested(string mode)
    {
        lock (ApplyLock)
        {
            if (mode != _requestedMode)
                return; // a newer config already superseded this one
            try
            {
                Apply(mode);
            }
            catch (Exception ex) // an exception escaping a worker thread would end the process
            {
                Console.WriteLine($"[launch-on-boot] Failed to apply '{mode}': {ex.Message}");
            }
        }
    }

    private static void Apply(string mode)
    {
        switch (mode)
        {
            case User:
                if (ElevatedStartupTask.SetEnabled(false))
                    StartupRegistration.SetEnabled(true);
                break;
            case Admin:
                if (ElevatedStartupTask.IsEnabled() || ElevatedStartupTask.SetEnabled(true))
                    StartupRegistration.SetEnabled(false);
                break;
            default:
                StartupRegistration.SetEnabled(false);
                ElevatedStartupTask.SetEnabled(false);
                break;
        }
    }
}
