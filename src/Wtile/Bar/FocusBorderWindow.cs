using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Config;
using Wtile.Core;
using Monitor = Wtile.Core.Monitor;

namespace Wtile.Bar;

/// <summary>
/// Draws a dwm/mango-style colored border around whichever managed window currently has OS
/// focus. Implemented as four thin click-through overlay windows (top/bottom/left/right strips)
/// rather than one window with per-pixel alpha -- simpler, and every strip is a single solid
/// color so there's no transparency compositing to get right. Repositioned/shown/hidden on
/// <see cref="WindowManager.Changed"/>, the same event BarWindow uses to know when to repaint, so
/// it stays in sync with focus changes, arranges, and tag switches for free.
/// </summary>
internal sealed unsafe class FocusBorderWindow : IDisposable
{
    private const string ClassName = "WtileFocusBorder";
    private const nuint RefreshTimerId = 1;
    private const uint RefreshIntervalMs = 150;
    private static readonly Dictionary<nint, FocusBorderWindow> Instances = [];
    private static bool _classRegistered;

    /// <summary>The repeating refresh timer's callback is a static function pointer and can't
    /// capture state (same reason WindowManager/HotkeyManager use a similar static-instance
    /// pattern) -- there's only ever one FocusBorderWindow for the whole app (focus is global, not
    /// per-monitor), so a single live instance registers itself here.</summary>
    private static FocusBorderWindow? _current;

    private readonly WindowManager _manager;
    private readonly HWND[] _strips = new HWND[4]; // top, bottom, left, right
    private int _width;
    private Color _color;
    private bool _visible;
    private RECT? _lastRect; // dedupes [border] log spam -- see Refresh()

    public FocusBorderWindow(WindowManager manager, int width, string colorHtml)
    {
        _manager = manager;
        _width = width;
        _color = ParseColor(colorHtml);

        EnsureClassRegistered();
        for (int i = 0; i < _strips.Length; i++)
        {
            _strips[i] = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW
                    | WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
                ClassName, "", WINDOW_STYLE.WS_POPUP,
                0, 0, 0, 0, HWND.Null, null, PInvoke.GetModuleHandle((string?)null), null);
            Instances[(nint)_strips[i].Value] = this;
            manager.IgnoredFocusHandles.Add(_strips[i]);
        }

        _current = this;
        manager.Changed += Refresh;
        Refresh();

        // A dragged/resized window's position/size changes continuously with no single reliable
        // WinEvent to hook: EVENT_OBJECT_LOCATIONCHANGE and EVENT_SYSTEM_MOVESIZESTART/END both
        // turned out to not fire for every window (observed: dragging didn't move the border at
        // all for some windows) -- and the same "window moved without an event Wtile reacts to"
        // gap shows up for other causes too, e.g. a VM host resizing the guest's display. Rather
        // than chase every possible cause individually, a short repeating poll just re-measures
        // and redraws unconditionally -- cheap (one DWM bounds query + a handful of SetWindowPos
        // calls on four small strips) and self-heals regardless of why the window moved.
        PInvoke.SetTimer(HWND.Null, RefreshTimerId, RefreshIntervalMs, &RefreshTimerProc);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void RefreshTimerProc(HWND hwnd, uint msg, nuint idEvent, uint dwTime) => _current?.Refresh();

    /// <summary>Applies a reloaded config in place. Called synchronously from the "reload" command.</summary>
    public void ApplyConfig(GeneralConfig config)
    {
        _width = config.FocusedBorderWidth;
        _color = ParseColor(config.FocusedBorderColor);
        Refresh();
    }

    private void Refresh()
    {
        ManagedWindow? focused = null;
        foreach (ManagedWindow candidate in _manager.Windows)
        {
            if (candidate.Handle == _manager.FocusedHandle)
            {
                focused = candidate;
                break;
            }
        }

        if (_width <= 0 || focused is null || focused.IsMinimized)
        {
            Hide();
            return;
        }

        // A window stays tracked (and FocusedHandle can still point at it) after a tag switch
        // hides it -- e.g. it was on tag 1, you switch to tag 2, and nothing else grabs OS focus
        // to fire a new EVENT_SYSTEM_FOREGROUND. Without this check the border keeps framing an
        // invisible window on a tag you've since left.
        Monitor monitor = _manager.Monitors[focused.MonitorIndex];
        if (!WindowManager.IsVisibleOn(focused, monitor, focused.MonitorIndex))
        {
            Hide();
            return;
        }

        RECT r = WindowInspector.GetVisibleBounds(focused.Handle);
        int w = r.right - r.left;
        int h = r.bottom - r.top;
        if (w <= 0 || h <= 0)
        {
            Hide();
            return;
        }

        // Temporary diagnostic: only logs when the measured rect actually changes (not every
        // ~150ms poll tick), so a burst of these while dragging confirms the poll is both firing
        // and picking up the live position; silence during a drag means it isn't.
        if (_lastRect is null || !_lastRect.Value.Equals(r))
        {
            Console.WriteLine($"[border] '{focused.Title}' -> ({r.left},{r.top}) {w}x{h}");
            _lastRect = r;
        }

        int bw = Math.Min(_width, Math.Min(w, h) / 2); // never let strips overlap past the window's own center
        Position(_strips[0], r.left, r.top, w, bw); // top
        Position(_strips[1], r.left, r.bottom - bw, w, bw); // bottom
        Position(_strips[2], r.left, r.top, bw, h); // left
        Position(_strips[3], r.right - bw, r.top, bw, h); // right

        foreach (HWND strip in _strips)
            PInvoke.InvalidateRect(strip, (RECT?)null, false);
        _visible = true;
    }

    private static void Position(HWND hwnd, int x, int y, int w, int h) =>
        PInvoke.SetWindowPos(
            hwnd, HWND.Null, x, y, w, h,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

    private void Hide()
    {
        if (!_visible)
            return;
        foreach (HWND strip in _strips)
            PInvoke.ShowWindow(strip, SHOW_WINDOW_CMD.SW_HIDE);
        _visible = false;
    }

    private static Color ParseColor(string html)
    {
        try
        {
            return string.IsNullOrWhiteSpace(html) ? ColorTranslator.FromHtml("#89b4fa") : ColorTranslator.FromHtml(html);
        }
        catch (Exception)
        {
            return ColorTranslator.FromHtml("#89b4fa");
        }
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
        if (msg == PInvoke.WM_PAINT && Instances.TryGetValue((nint)hwnd.Value, out FocusBorderWindow? self))
        {
            self.OnPaint(hwnd);
            return new LRESULT(0);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnPaint(HWND hwnd)
    {
        PAINTSTRUCT ps;
        HDC hdc = PInvoke.BeginPaint(hwnd, &ps);
        RECT clientRect;
        PInvoke.GetClientRect(hwnd, &clientRect);

        HBRUSH brush = PInvoke.CreateSolidBrush((COLORREF)(uint)ColorTranslator.ToWin32(_color));
        PInvoke.FillRect(hdc, &clientRect, brush);
        PInvoke.DeleteObject(brush);

        PInvoke.EndPaint(hwnd, &ps);
    }

    public void Dispose()
    {
        PInvoke.KillTimer(HWND.Null, RefreshTimerId);
        _current = null;
        foreach (HWND strip in _strips)
        {
            Instances.Remove((nint)strip.Value);
            PInvoke.DestroyWindow(strip);
        }
    }
}
