using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Com;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Shell;

namespace Wtile.Core;

// Never throws: a launch that fails is logged, never allowed to take the window manager down.
internal static unsafe class ProcessLauncher
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

    /// <summary>
    /// Launches <paramref name="launchTarget"/> (a PATH exe/bat/cmd, or a Start Menu .lnk) at the
    /// interactive user's own, unelevated privilege level -- even when this process itself is
    /// running elevated. A plain Process.Start/ShellExecute would otherwise hand the launched app
    /// the same admin token Wtile runs with, by ordinary CreateProcess inheritance; Windows has no
    /// "de-elevate" API to undo that. Instead this borrows explorer.exe's own token: UAC's
    /// split-token design keeps explorer unelevated even under an administrator account, so
    /// duplicating its primary token and starting the new process with CreateProcessWithTokenW
    /// produces exactly the unelevated process a normal double-click in Explorer would. Falls back
    /// to a normal (elevated) launch if any step fails -- e.g. SeImpersonatePrivilege unavailable --
    /// so the launcher always launches something rather than silently doing nothing.
    /// </summary>
    public static void TryStartDeElevated(string launchTarget, string logTag)
    {
        if (!Environment.IsPrivilegedProcess)
        {
            TryStart(new ProcessStartInfo(launchTarget) { UseShellExecute = true }, logTag);
            return;
        }

        try
        {
            (string path, string arguments, string? workingDirectory) = ResolveLaunchTarget(launchTarget);
            StartWithExplorerToken(path, arguments, workingDirectory);
        }
        catch (Exception ex) when (ex is Win32Exception or COMException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[{logTag}] De-elevated launch of '{launchTarget}' failed ({ex.Message}); launching elevated instead.");
            TryStart(new ProcessStartInfo(launchTarget) { UseShellExecute = true }, logTag);
        }
    }

    // CreateProcess (unlike ShellExecute) can't run a .lnk or a .bat/.cmd directly -- it needs an
    // actual executable image -- so a shortcut is resolved to its real target first, and a batch
    // file is handed to cmd.exe /c the same way the shell associates it.
    private static (string Path, string Arguments, string? WorkingDirectory) ResolveLaunchTarget(string launchTarget)
    {
        string path = launchTarget;
        string arguments = "";
        string? workingDirectory = null;

        if (string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
            (path, arguments, workingDirectory) = ResolveShortcut(path);

        string ext = Path.GetExtension(path);
        if (string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase))
        {
            arguments = arguments.Length > 0 ? $"/c \"{path}\" {arguments}" : $"/c \"{path}\"";
            path = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        }

        return (path, arguments, workingDirectory);
    }

    private static (string Target, string Arguments, string? WorkingDirectory) ResolveShortcut(string lnkPath)
    {
        IShellLinkW shellLink = ShellLink.CreateInstance<IShellLinkW>();
        ((IPersistFile)shellLink).Load(lnkPath, STGM.STGM_READ);

        Span<char> targetBuffer = stackalloc char[260]; // MAX_PATH
        var findData = default(WIN32_FIND_DATAW);
        shellLink.GetPath(targetBuffer, ref findData, 0);

        Span<char> argsBuffer = stackalloc char[1024];
        shellLink.GetArguments(argsBuffer);

        Span<char> dirBuffer = stackalloc char[260]; // MAX_PATH
        shellLink.GetWorkingDirectory(dirBuffer);

        string workingDirectory = dirBuffer.ToTrimmedString();
        return (targetBuffer.ToTrimmedString(), argsBuffer.ToTrimmedString(), workingDirectory.Length > 0 ? workingDirectory : null);
    }

    private static string ToTrimmedString(this Span<char> buffer)
    {
        int nullIndex = buffer.IndexOf('\0');
        return new string(nullIndex >= 0 ? buffer[..nullIndex] : buffer);
    }

    private static void StartWithExplorerToken(string path, string arguments, string? workingDirectory)
    {
        HWND shellWindow = PInvoke.GetShellWindow();
        if (shellWindow.IsNull)
            throw new Win32Exception("No shell window (explorer.exe) is running.");

        PInvoke.GetWindowThreadProcessId(shellWindow, out uint explorerPid);
        if (explorerPid == 0)
            throw new Win32Exception("Could not resolve explorer.exe's process id.");

        HANDLE explorerProcessHandle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, explorerPid);
        if (explorerProcessHandle.IsNull)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using var explorerProcess = new SafeFileHandle((IntPtr)explorerProcessHandle.Value, ownsHandle: true);

        if (!PInvoke.OpenProcessToken(explorerProcess, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, out SafeFileHandle explorerToken))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (explorerToken)
        {
            const TOKEN_ACCESS_MASK duplicateAccess = TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE
                | TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_ADJUST_DEFAULT | TOKEN_ACCESS_MASK.TOKEN_ADJUST_SESSIONID;
            if (!PInvoke.DuplicateTokenEx(explorerToken, duplicateAccess, null, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out SafeFileHandle primaryToken))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using (primaryToken)
            {
                CreateProcessWithToken(primaryToken, path, arguments, workingDirectory);
            }
        }
    }

    private static void CreateProcessWithToken(SafeHandle token, string path, string arguments, string? workingDirectory)
    {
        string commandLine = arguments.Length > 0 ? $"\"{path}\" {arguments}\0" : $"\"{path}\"\0";
        Span<char> commandLineBuffer = commandLine.ToCharArray();
        var startupInfo = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };

        bool started = PInvoke.CreateProcessWithToken(
            token, CREATE_PROCESS_LOGON_FLAGS.LOGON_WITH_PROFILE,
            lpApplicationName: null, ref commandLineBuffer, dwCreationFlags: default,
            lpEnvironment: null, lpCurrentDirectory: workingDirectory,
            lpStartupInfo: startupInfo, out PROCESS_INFORMATION processInfo);
        if (!started)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        PInvoke.CloseHandle(processInfo.hProcess);
        PInvoke.CloseHandle(processInfo.hThread);
    }
}
