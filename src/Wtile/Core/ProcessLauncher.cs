using System.ComponentModel;
using System.Diagnostics;

namespace Wtile.Core;

// Never throws: a launch that fails is logged, never allowed to take the window manager down.
internal static class ProcessLauncher
{
    private const int ErrorNoAssociation = 1155;

    public static void TryStart(ProcessStartInfo startInfo, string logTag)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{logTag}] Failed to start '{startInfo.FileName}': {ex.Message}");
        }
    }

    // Opens a file in its default app, falling back to Notepad when nothing is associated with it.
    public static void OpenFile(string path, string logTag)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorNoAssociation)
        {
            TryStart(new ProcessStartInfo("notepad.exe") { ArgumentList = { path } }, logTag);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{logTag}] Failed to open '{path}': {ex.Message}");
        }
    }
}
