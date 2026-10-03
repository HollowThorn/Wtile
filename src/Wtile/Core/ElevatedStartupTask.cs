using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Wtile.Core;

// A per-user Run key entry is always started with a standard token, so starting elevated at
// login needs a scheduled task instead.
internal static class ElevatedStartupTask
{
    private const string TaskName = "Wtile";
    private const int ErrorCancelled = 1223;

    public static bool IsEnabled()
    {
        if (Environment.ProcessPath is not string exePath)
            return false;
        (int exitCode, string output) = RunSchtasks($"/Query /TN \"{TaskName}\" /XML");
        return exitCode == 0
            && string.Equals(StartupTaskXml.ReadCommand(output), exePath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool SetEnabled(bool enabled) =>
        enabled
            ? Register()
            : !TaskExists() || RunSchtasksElevated($"/Delete /TN \"{TaskName}\" /F", "Removed elevated logon task.");

    private static bool Register()
    {
        if (Environment.ProcessPath is not string exePath)
        {
            Console.WriteLine("[launch-on-boot] Can't resolve this exe's path; elevated task not registered.");
            return false;
        }

        string xmlPath = Path.Combine(Path.GetTempPath(), $"wtile-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, StartupTaskXml.Build($"{Environment.UserDomainName}\\{Environment.UserName}", exePath), Encoding.Unicode);
            return RunSchtasksElevated($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F", $"Registered elevated logon task for {exePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[launch-on-boot] Failed to write the task definition: {ex.Message}");
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool TaskExists() => RunSchtasks($"/Query /TN \"{TaskName}\"").ExitCode == 0;

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            });
            if (process is null)
                return (-1, "");
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return (-1, "");
        }
    }

    // Creating or deleting a highest-privilege task needs admin rights: a non-elevated Wtile asks
    // once through UAC instead of failing.
    private static bool RunSchtasksElevated(string arguments, string successMessage)
    {
        int exitCode = Environment.IsPrivilegedProcess ? RunSchtasks(arguments).ExitCode : RunSchtasksThroughUac(arguments);
        if (exitCode == 0)
            Console.WriteLine($"[launch-on-boot] {successMessage}");
        else if (exitCode != ErrorCancelled)
            Console.WriteLine($"[launch-on-boot] schtasks {arguments} failed (exit {exitCode}).");
        return exitCode == 0;
    }

    private static int RunSchtasksThroughUac(string arguments)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null)
                return -1;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            Console.WriteLine("[launch-on-boot] Administrator permission was declined; nothing changed.");
            return ErrorCancelled;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return -1;
        }
    }
}
