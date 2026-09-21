using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Commands;

namespace Wtile.Core;

/// <summary>
/// A single notification-area icon for the process's whole lifetime (Program.cs owns the one
/// instance), same as Steam/Teams running with no visible window of their own. Right-click opens
/// a small menu that just calls into existing commands -- "reload"/"quit", the same ones
/// Win+Shift+R/Win+Shift+Q already run, plus a checkable "Launch on boot" item over
/// "toggle-launch-on-boot" -- rather than duplicating that logic. Left-click does
/// nothing: there's no window to restore, so unlike Steam/Teams there's nothing useful to do on a
/// plain click.
/// </summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const string ClassName = "WtileTrayWindow";

    /// <summary>WM_APP (0x8000) + 4 (see HotkeyManager for +1/+2's owners): the tray icon's
    /// shell callback message, packing the originating mouse message into the low word of lParam.</summary>
    private const uint WM_APP_TRAYICON = 0x8000 + 4;

    private const uint MenuIdReload = 1;
    private const uint MenuIdQuit = 2;
    private const uint MenuIdLaunchOnBoot = 3;

    /// <summary>RT_GROUP_ICON id the .NET SDK embeds Wtile.csproj's &lt;ApplicationIcon&gt; under
    /// (same ordinal as IDI_APPLICATION, which is coincidental -- this loads Wtile's own icon out
    /// of its own module, not the system one).</summary>
    private const uint AppIconResourceId = 32512;

    private static readonly Dictionary<nint, TrayIcon> Instances = [];
    private static bool _classRegistered;

    private readonly HWND _hwnd;
    private readonly CommandRegistry _commands;
    private NOTIFYICONDATAW _data;

    public TrayIcon(CommandRegistry commands)
    {
        _commands = commands;
        EnsureClassRegistered();

        // HWND_MESSAGE = (HWND)-3, same message-only-window trick HotkeyManager uses -- this icon
        // needs a real HWND to receive Shell_NotifyIcon's callback message, but never shows a window.
        var hwndMessage = new HWND((void*)(nint)(-3));
        _hwnd = PInvoke.CreateWindowEx(
            0, ClassName, "Wtile Tray", 0,
            0, 0, 0, 0,
            hwndMessage, null, PInvoke.GetModuleHandle((string?)null), null);
        Instances[(nint)_hwnd.Value] = this;

        _data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
            uCallbackMessage = WM_APP_TRAYICON,
            hIcon = PInvoke.LoadIcon(new HINSTANCE(PInvoke.GetModuleHandle((PCWSTR)null).Value), (PCWSTR)(char*)AppIconResourceId),
            szTip = "Wtile",
        };
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in _data);
    }

    private void ShowContextMenu()
    {
        using DestroyMenuSafeHandle menu = PInvoke.CreatePopupMenu_SafeHandle();
        // Check state is read fresh each time the menu opens, so it stays honest if the Run entry
        // was changed from outside (config reload, or the user editing the registry directly).
        MENU_ITEM_FLAGS launchOnBootFlags = MENU_ITEM_FLAGS.MF_STRING
            | (StartupRegistration.IsEnabled() ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED);
        PInvoke.AppendMenu(menu, launchOnBootFlags, MenuIdLaunchOnBoot, "Launch on boot");
        PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuIdReload, "Reload config");
        PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuIdQuit, "Quit Wtile");

        PInvoke.GetCursorPos(out System.Drawing.Point cursor);
        // A popup menu tracked from a message-only window won't dismiss itself on an outside
        // click unless *some* window owns the foreground first -- standard tray-icon dance.
        PInvoke.SetForegroundWindow(_hwnd);
        PInvoke.TrackPopupMenu(menu, TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON, cursor.X, cursor.Y, _hwnd, null);
    }

    private static void EnsureClassRegistered()
    {
        if (_classRegistered)
            return;
        _classRegistered = true;

        fixed (char* classNamePtr = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = new HINSTANCE(PInvoke.GetModuleHandle((PCWSTR)null).Value),
                lpszClassName = classNamePtr,
            };
            PInvoke.RegisterClassEx(&wc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (!Instances.TryGetValue((nint)hwnd.Value, out TrayIcon? self))
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        if (msg == WM_APP_TRAYICON)
        {
            uint mouseMsg = unchecked((uint)(lParam.Value & 0xFFFF));
            if (mouseMsg == PInvoke.WM_RBUTTONUP || mouseMsg == PInvoke.WM_LBUTTONUP)
                self.ShowContextMenu();
            return new LRESULT(0);
        }
        if (msg == PInvoke.WM_COMMAND)
        {
            uint id = unchecked((uint)(wParam.Value & 0xFFFF));
            if (id == MenuIdLaunchOnBoot)
                self._commands.TryExecute("toggle-launch-on-boot", []);
            else if (id == MenuIdReload)
                self._commands.TryExecute("reload", []);
            else if (id == MenuIdQuit)
                self._commands.TryExecute("quit", []);
            return new LRESULT(0);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in _data);
        Instances.Remove((nint)_hwnd.Value);
        PInvoke.DestroyWindow(_hwnd);
    }
}
