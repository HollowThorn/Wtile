using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Bar.Segments;
using Wtile.Commands;
using Wtile.Config;
using Wtile.Core;
using Wtile.Layouts;
using Monitor = Wtile.Core.Monitor;

namespace Wtile.Bar;

/// <summary>
/// A single top/bottom bar window on one monitor -- Program.cs creates one per entry in
/// <see cref="WindowManager.Monitors"/>. Owns the HWND/WndProc, delegates pixel drawing to
/// <see cref="BarRenderer"/>, and applies config reloads in place (colors, font, height,
/// position, segment list) via <see cref="ApplyConfig"/> -- called synchronously from the
/// "reload" command (Commands/), which already runs on the main thread by the time a
/// hotkey-triggered command executes.
/// </summary>
internal sealed unsafe class BarWindow : IDisposable
{
    private const string ClassName = "WtileBarWindow";
    private const int TimerIdClock = 1;

    private static readonly Dictionary<nint, BarWindow> Instances = [];
    private static bool _classRegistered;

    private readonly HWND _hwnd;
    private readonly WindowManager _manager;
    private readonly CommandRegistry _commands;
    private readonly Monitor _monitor;
    private readonly int _monitorIndex;
    private readonly BarTheme _theme;
    private Font _font;
    private BarRenderer _renderer = null!;
    private List<SegmentHit> _lastHits = [];
    private int _height;
    private bool _isBottom;

    public BarWindow(WindowManager manager, CommandRegistry commands, BarConfig config, Monitor monitor, int monitorIndex)
    {
        _manager = manager;
        _commands = commands;
        _monitor = monitor;
        _monitorIndex = monitorIndex;
        _theme = new BarTheme(config.Colors);
        _font = new Font(config.FontFamily, (float)config.FontSize);

        EnsureClassRegistered();

        LayoutRect bounds = WindowInspector.GetMonitorBounds(monitor.Handle);
        _height = config.Height;
        _isBottom = config.Position == "bottom";
        int y = _isBottom ? bounds.Y + bounds.Height - _height : bounds.Y;

        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            ClassName,
            "Wtile Bar",
            WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
            bounds.X, y, bounds.Width, _height,
            HWND.Null, null, PInvoke.GetModuleHandle((string?)null), null);

        Instances[(nint)_hwnd.Value] = this;
        manager.IgnoredFocusHandles.Add(_hwnd);

        RebuildSegments(config.Segments);
        ApplyReservedInset();

        manager.Changed += () => PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
        PInvoke.SetTimer(_hwnd, TimerIdClock, 1000, null);
    }

    /// <summary>Applies a reloaded bar config in place. Called synchronously from the "reload" command.</summary>
    public void ApplyConfig(BarConfig config)
    {
        _theme.Apply(config.Colors);

        if (!string.Equals(_font.FontFamily.Name, config.FontFamily, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(_font.Size - (float)config.FontSize) > 0.01f)
        {
            Font old = _font;
            _font = new Font(config.FontFamily, (float)config.FontSize);
            old.Dispose();
        }

        RebuildSegments(config.Segments);

        bool nowBottom = config.Position == "bottom";
        if (_height != config.Height || _isBottom != nowBottom)
        {
            _height = config.Height;
            _isBottom = nowBottom;
            RepositionAndResize();
        }

        ApplyReservedInset();
        PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
    }

    /// <summary>
    /// Unconditionally re-reads the primary monitor's current dimensions and repositions/resizes
    /// the bar + re-arranges windows to match -- fixes a stale bar size/window layout after a
    /// monitor/resolution change, independent of whether the config file itself changed. Called
    /// from the "reload" command (Win+Shift+R by default) rather than automatically, since that's
    /// a rare enough event not to warrant a background watcher for it either.
    /// </summary>
    public void RefreshGeometry()
    {
        RepositionAndResize();
        ApplyReservedInset();
        PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
    }

    private void RepositionAndResize()
    {
        LayoutRect bounds = WindowInspector.GetMonitorBounds(_monitor.Handle);
        int y = _isBottom ? bounds.Y + bounds.Height - _height : bounds.Y;
        PInvoke.SetWindowPos(
            _hwnd, HWND.Null, bounds.X, y, bounds.Width, _height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
    }

    private void ApplyReservedInset()
    {
        _monitor.ReservedTopInset = _isBottom ? 0 : _height;
        _monitor.ReservedBottomInset = _isBottom ? _height : 0;
        _manager.Arrange();
    }

    private void RebuildSegments(BarSegmentsConfig segmentsConfig)
    {
        _renderer = new BarRenderer(BuildSegments(segmentsConfig.Left), BuildSegments(segmentsConfig.Right), _theme);
    }

    private ISegment[] BuildSegments(IEnumerable<string> names)
    {
        var result = new List<ISegment>();
        foreach (string name in names)
        {
            ISegment? segment = CreateSegment(name);
            if (segment is not null)
                result.Add(segment);
            else
                Console.WriteLine($"[bar] Unknown segment '{name}' in config, skipping.");
        }
        return [.. result];
    }

    private ISegment? CreateSegment(string name) => name switch
    {
        "tags" => new TagsSegment(_manager, _commands, _theme, _monitorIndex),
        "layout-symbol" => new LayoutSymbolSegment(_manager, _theme, _monitorIndex),
        "window-title" => new TitleSegment(_manager, _theme),
        "clock" => new ClockSegment(_theme),
        _ => null,
    };

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
                hCursor = PInvoke.LoadCursor(HINSTANCE.Null, PInvoke.IDC_ARROW),
                lpszClassName = classNamePtr,
            };
            PInvoke.RegisterClassEx(&wc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (!Instances.TryGetValue((nint)hwnd.Value, out BarWindow? self))
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        switch (msg)
        {
            case PInvoke.WM_PAINT:
                self.OnPaint();
                return new LRESULT(0);
            case PInvoke.WM_LBUTTONUP:
                self.OnClick(lParam);
                return new LRESULT(0);
            case PInvoke.WM_TIMER:
                PInvoke.InvalidateRect(hwnd, (RECT?)null, false);
                return new LRESULT(0);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnPaint()
    {
        PAINTSTRUCT ps;
        HDC hdc = PInvoke.BeginPaint(_hwnd, &ps);
        RECT clientRect;
        PInvoke.GetClientRect(_hwnd, &clientRect);
        int w = clientRect.right - clientRect.left;
        int h = clientRect.bottom - clientRect.top;

        if (w > 0 && h > 0)
        {
            using var buffer = new Bitmap(w, h);
            using (Graphics bufferGraphics = Graphics.FromImage(buffer))
            {
                bufferGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                _lastHits = _renderer.Draw(bufferGraphics, _font, new RectangleF(0, 0, w, h));
            }
            using Graphics screenGraphics = Graphics.FromHdc(hdc);
            screenGraphics.DrawImageUnscaled(buffer, 0, 0);
        }

        PInvoke.EndPaint(_hwnd, &ps);
    }

    private void OnClick(LPARAM lParam)
    {
        long raw = lParam.Value;
        int x = unchecked((short)(raw & 0xFFFF));

        foreach (SegmentHit hit in _lastHits)
        {
            if (x >= hit.Bounds.Left && x < hit.Bounds.Right)
            {
                hit.Segment.OnClick(x - hit.Bounds.Left);
                break;
            }
        }
    }

    public void Dispose()
    {
        PInvoke.KillTimer(_hwnd, TimerIdClock);
        Instances.Remove((nint)_hwnd.Value);
        PInvoke.DestroyWindow(_hwnd);
        _font.Dispose();
    }
}
