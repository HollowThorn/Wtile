using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Commands;
using Wtile.Config;

namespace Wtile.Hotkeys;

/// <summary>
/// Dispatches global hotkeys via a low-level keyboard hook (WH_KEYBOARD_LL) rather than
/// RegisterHotKey. Windows reserves nearly the entire bare Win+&lt;key&gt; namespace for the
/// shell (Explorer registers it first at boot), so RegisterHotKey fails for almost all of them
/// -- a low-level hook intercepts the keystroke before Explorer's own hotkey table ever sees
/// it, sidestepping the conflict entirely. This is the same technique bug.n uses. A small number
/// of OS-hardened sequences are immune to *any*
/// hook, hardcoded below Explorer and even below other hooks: Win+L (lock workstation) and
/// Ctrl+Alt+Delete (the Secure Attention Sequence) -- expect those two to never be bindable.
///
/// The hook callback itself only decides swallow-or-not and must return fast (Windows can
/// silently drop a slow low-level hook and lets input lag system-wide while it runs), so actual
/// command execution is deferred: it PostMessages a hidden message-only window (HWND_MESSAGE)
/// and runs the command from there, back on the normal message loop.
/// </summary>
internal sealed unsafe class HotkeyManager : IDisposable
{
    private const string ClassName = "WtileHotkeyWindow";

    /// <summary>WM_APP (0x8000) + 2 (BarWindow's reload message is +1): the vk (low 32 bits) and
    /// modifiers (high 32 bits) are packed into LPARAM so the hook callback stays allocation-free.</summary>
    private const uint WM_APP_HOTKEY_FIRED = 0x8000 + 2;

    private static readonly Dictionary<nint, HotkeyManager> Instances = [];
    private static bool _classRegistered;

    /// <summary>The hook callback is a static function pointer and can't capture state, so (as with
    /// WindowManager/BarWindow) a single live instance registers itself here.</summary>
    private static HotkeyManager? _activeHook;

    private readonly HWND _hwnd;
    private readonly CommandRegistry _commands;
    private readonly Dictionary<KeyCombo, HotkeyBinding> _bindings = [];
    private HHOOK _hook;

    /// <summary>Set when a Win-modified hotkey fires and cleared on the matching Win key-up (see
    /// <see cref="DisguiseWinKeyUp"/>) -- tracks whether the *next* Win release still needs disguising.</summary>
    private bool _winKeyDisguisePending;

    public HotkeyManager(CommandRegistry commands)
    {
        _commands = commands;
        EnsureClassRegistered();

        // HWND_MESSAGE = (HWND)-3, a documented Win32 pseudo-handle for message-only windows.
        var hwndMessage = new HWND((void*)(nint)(-3));
        _hwnd = PInvoke.CreateWindowEx(
            0, ClassName, "Wtile Hotkeys", 0,
            0, 0, 0, 0,
            hwndMessage, null, PInvoke.GetModuleHandle((string?)null), null);
        Instances[(nint)_hwnd.Value] = this;

        _activeHook = this;
        _hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, &LowLevelKeyboardProc, HINSTANCE.Null, 0);
        if (_hook.IsNull)
            Console.WriteLine("[hotkeys] Failed to install the low-level keyboard hook; hotkeys will not work.");
    }

    /// <summary>Replaces the whole binding set from a (re)loaded config. On a duplicate combo,
    /// the last entry wins -- same rule as CommandRegistry.</summary>
    public void ApplyBindings(IReadOnlyList<HotkeyBinding> bindings)
    {
        _bindings.Clear();
        foreach (HotkeyBinding binding in bindings)
        {
            if (KeyComboParser.TryParse(binding.Keys, out KeyCombo combo))
                _bindings[combo] = binding; // ConfigLoader already filters unparseable ones, but stay defensive
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        HotkeyManager? self = _activeHook;
        if (nCode == 0 && self is not null)
        {
            var data = (KBDLLHOOKSTRUCT*)lParam.Value;
            if (wParam.Value == PInvoke.WM_KEYDOWN || wParam.Value == PInvoke.WM_SYSKEYDOWN)
            {
                var combo = new KeyCombo(CurrentModifiers(), data->vkCode);
                if (self._bindings.ContainsKey(combo))
                {
                    if ((combo.Modifiers & HotkeyModifiers.Win) != 0)
                        self._winKeyDisguisePending = true;
                    nint packed = (nint)combo.VirtualKey | ((nint)(uint)combo.Modifiers << 32);
                    PInvoke.PostMessage(self._hwnd, WM_APP_HOTKEY_FIRED, 0, packed);
                    return new LRESULT(1); // swallow -- keeps it from reaching Explorer/the focused app
                }
            }
            else if ((wParam.Value == PInvoke.WM_KEYUP || wParam.Value == PInvoke.WM_SYSKEYUP) && self._winKeyDisguisePending
                && (data->vkCode == (uint)VIRTUAL_KEY.VK_LWIN || data->vkCode == (uint)VIRTUAL_KEY.VK_RWIN))
            {
                self._winKeyDisguisePending = false;
                DisguiseWinKeyUp((VIRTUAL_KEY)data->vkCode);
                return new LRESULT(1); // swallow the real key-up; a synthetic one follows the disguise tap below
            }
        }
        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    /// <summary>
    /// Windows opens the Start Menu whenever a bare Win press+release completes without the
    /// shell ever seeing another key go down alongside it. Since the hook above swallows the
    /// hotkey's own key (e.g. the "Q" in Win+Q) before it reaches Explorer's own keyboard hook,
    /// Explorer never learns Win was combined with anything -- so releasing Win right after the
    /// hotkey fires looks, to the shell, exactly like a bare Win tap, and the Start Menu pops
    /// open. The fix: swallow the real Win-up, inject a harmless Ctrl tap so Explorer's hook sees
    /// "something else was pressed", then inject the Win-up itself so the rest of the system
    /// still sees Win go up normally.
    /// </summary>
    private static unsafe void DisguiseWinKeyUp(VIRTUAL_KEY winKey)
    {
        Span<INPUT> inputs =
        [
            KeyInput(VIRTUAL_KEY.VK_CONTROL, down: true),
            KeyInput(VIRTUAL_KEY.VK_CONTROL, down: false),
            KeyInput(winKey, down: false),
        ];
        PInvoke.SendInput(inputs, sizeof(INPUT));
    }

    private static INPUT KeyInput(VIRTUAL_KEY vk, bool down) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
            },
        },
    };

    private static HotkeyModifiers CurrentModifiers()
    {
        HotkeyModifiers mods = HotkeyModifiers.None;
        if (IsDown(VIRTUAL_KEY.VK_LWIN) || IsDown(VIRTUAL_KEY.VK_RWIN)) mods |= HotkeyModifiers.Win;
        if (IsDown(VIRTUAL_KEY.VK_CONTROL)) mods |= HotkeyModifiers.Control;
        if (IsDown(VIRTUAL_KEY.VK_SHIFT)) mods |= HotkeyModifiers.Shift;
        if (IsDown(VIRTUAL_KEY.VK_MENU)) mods |= HotkeyModifiers.Alt;
        return mods;
    }

    // GetAsyncKeyState reads the real-time physical key state directly, independent of any
    // thread's message queue -- the right choice inside a hook callback invoked synchronously
    // by the raw input path.
    private static bool IsDown(VIRTUAL_KEY vk) => (PInvoke.GetAsyncKeyState((int)vk) & 0x8000) != 0;

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
        if (!Instances.TryGetValue((nint)hwnd.Value, out HotkeyManager? self))
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        if (msg == WM_APP_HOTKEY_FIRED)
        {
            long packed = lParam.Value;
            uint vk = unchecked((uint)(packed & 0xFFFFFFFF));
            var modifiers = (HotkeyModifiers)unchecked((uint)(packed >> 32));
            if (self._bindings.TryGetValue(new KeyCombo(modifiers, vk), out HotkeyBinding? binding))
                self._commands.TryExecute(binding.Command, binding.Args);
            return new LRESULT(0);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_hook.IsNull)
        {
            PInvoke.UnhookWindowsHookEx(_hook);
            _hook = default;
        }
        _activeHook = null;
        Instances.Remove((nint)_hwnd.Value);
        PInvoke.DestroyWindow(_hwnd);
    }
}
