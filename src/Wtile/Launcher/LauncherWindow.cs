using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Bar;
using Wtile.Config;
using Wtile.Core;
using Wtile.Layouts;
using Monitor = Wtile.Core.Monitor;

namespace Wtile.Launcher;

/// <summary>
/// dmenu-style app launcher: a single-line popup spanning the current monitor's width, flush
/// against whichever edge the bar reserves (below a top bar, above a bottom bar). Unlike every
/// other Wtile overlay (BarWindow, FocusBorderWindow), this window needs real OS keyboard focus,
/// so it's deliberately not WS_EX_NOACTIVATE and drives its own text input by hand (WM_CHAR/
/// WM_KEYDOWN) rather than a native Edit control -- there's no WinForms/common-controls dependency
/// anywhere else in this codebase, and hand-rolled input keeps this window in the same
/// raw-Win32-plus-GDI+ style as BarWindow.
/// </summary>
internal sealed unsafe class LauncherWindow : IDisposable
{
    private const string ClassName = "WtileLauncher";
    private static readonly Dictionary<nint, LauncherWindow> Instances = [];
    private static bool _classRegistered;

    private readonly HWND _hwnd;
    private readonly WindowManager _manager;
    private readonly BarTheme _theme;
    private Font _font;
    private int _height;

    private List<LauncherEntry> _allEntries = [];
    private List<LauncherEntry> _filtered = [];
    private List<(LauncherEntry Entry, RectangleF Bounds)> _lastHits = [];
    private string _query = "";
    private int _selectedIndex;
    private bool _visible;
    private HWND _previousForeground;

    /// <summary>Guards the synchronous WM_ACTIVATE that firing SetForegroundWindow/SetFocus sends
    /// to this same window during Show() -- without it, that self-activation would be read as a
    /// deactivation and immediately re-hide the window it's still in the middle of showing.</summary>
    private bool _showing;

    public LauncherWindow(WindowManager manager, BarConfig config)
    {
        _manager = manager;
        _theme = new BarTheme(config.Colors);
        _font = new Font(config.FontFamily, (float)config.FontSize);
        _height = config.Height;

        EnsureClassRegistered();

        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            ClassName, "Wtile Launcher", WINDOW_STYLE.WS_POPUP,
            0, 0, 0, 0, HWND.Null, null, PInvoke.GetModuleHandle((string?)null), null);

        Instances[(nint)_hwnd.Value] = this;
        manager.IgnoredFocusHandles.Add(_hwnd);
    }

    /// <summary>Applies a reloaded bar config in place -- same font/color/height source the bar
    /// itself uses, so the launcher stays visually consistent with it. Called synchronously from
    /// the "reload" command.</summary>
    public void ApplyConfig(BarConfig config)
    {
        _theme.Apply(config.Colors);
        _height = config.Height;

        if (!string.Equals(_font.FontFamily.Name, config.FontFamily, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(_font.Size - (float)config.FontSize) > 0.01f)
        {
            Font old = _font;
            _font = new Font(config.FontFamily, (float)config.FontSize);
            old.Dispose();
        }
    }

    public void Toggle()
    {
        if (_visible)
            Hide();
        else
            Show();
    }

    private void Show()
    {
        _previousForeground = PInvoke.GetForegroundWindow();

        _allEntries = AppCatalog.Build();
        _query = "";
        _selectedIndex = 0;
        Refilter();

        Monitor monitor = _manager.Monitors[_manager.CurrentMonitorIndex];
        LayoutRect bounds = WindowInspector.GetMonitorBounds(monitor.Handle);
        int y = monitor.ReservedBottomInset > 0 && monitor.ReservedTopInset == 0
            ? bounds.Y + bounds.Height - monitor.ReservedBottomInset - _height
            : bounds.Y + monitor.ReservedTopInset;

        PInvoke.SetWindowPos(
            _hwnd, HWND.Null, bounds.X, y, bounds.Width, _height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        _visible = true;

        _showing = true;
        WindowInspector.ForceSetForegroundWindow(_hwnd);
        PInvoke.SetFocus(_hwnd);
        _showing = false;

        PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
    }

    private void Hide()
    {
        if (!_visible)
            return;
        _visible = false;
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
        if (!_previousForeground.IsNull)
            WindowInspector.ForceSetForegroundWindow(_previousForeground);
    }

    private void Refilter()
    {
        _filtered = _allEntries
            .Select(e => (Entry: e, Score: FuzzyMatcher.Score(_query, e.DisplayName)))
            .Where(t => t.Score.HasValue)
            .OrderByDescending(t => t.Score!.Value)
            .ThenBy(t => t.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Entry)
            .ToList();
        _selectedIndex = 0;
    }

    private void MoveSelection(int delta)
    {
        if (_filtered.Count == 0)
            return;
        _selectedIndex = ((_selectedIndex + delta) % _filtered.Count + _filtered.Count) % _filtered.Count;
        PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
    }

    private void LaunchSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filtered.Count)
            return;
        Launch(_filtered[_selectedIndex]);
        Hide();
    }

    private static void Launch(LauncherEntry entry)
    {
        try
        {
            Process.Start(new ProcessStartInfo(entry.LaunchTarget) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[launcher] Failed to start '{entry.LaunchTarget}': {ex.Message}");
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
                hCursor = PInvoke.LoadCursor(HINSTANCE.Null, PInvoke.IDC_ARROW),
                lpszClassName = classNamePtr,
            };
            PInvoke.RegisterClassEx(&wc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (!Instances.TryGetValue((nint)hwnd.Value, out LauncherWindow? self))
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        switch (msg)
        {
            case PInvoke.WM_PAINT:
                self.OnPaint();
                return new LRESULT(0);
            case PInvoke.WM_CHAR:
                self.OnChar((char)(uint)wParam.Value);
                return new LRESULT(0);
            case PInvoke.WM_KEYDOWN:
                self.OnKeyDown((VIRTUAL_KEY)(uint)wParam.Value);
                return new LRESULT(0);
            case PInvoke.WM_LBUTTONUP:
                self.OnClick(lParam);
                return new LRESULT(0);
            case PInvoke.WM_ACTIVATE:
                ulong activateState = wParam.Value;
                if (!self._showing && (activateState & 0xFFFF) == PInvoke.WA_INACTIVE)
                    self.Hide();
                return new LRESULT(0);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnChar(char c)
    {
        if (c < 0x20 || c == 0x7F) // Backspace/Tab/Enter/Escape are handled in OnKeyDown instead
            return;
        _query += c;
        Refilter();
        PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
    }

    private void OnKeyDown(VIRTUAL_KEY vk)
    {
        switch (vk)
        {
            case VIRTUAL_KEY.VK_BACK:
                if (_query.Length > 0)
                {
                    _query = _query[..^1];
                    Refilter();
                    PInvoke.InvalidateRect(_hwnd, (RECT?)null, false);
                }
                break;
            case VIRTUAL_KEY.VK_ESCAPE:
                Hide();
                break;
            case VIRTUAL_KEY.VK_RETURN:
                LaunchSelected();
                break;
            case VIRTUAL_KEY.VK_TAB:
                MoveSelection((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0 ? -1 : 1);
                break;
            case VIRTUAL_KEY.VK_RIGHT:
                MoveSelection(1);
                break;
            case VIRTUAL_KEY.VK_LEFT:
                MoveSelection(-1);
                break;
        }
    }

    private void OnClick(LPARAM lParam)
    {
        long raw = lParam.Value;
        int x = unchecked((short)(raw & 0xFFFF));

        foreach ((LauncherEntry entry, RectangleF bounds) in _lastHits)
        {
            if (x >= bounds.Left && x < bounds.Right)
            {
                Launch(entry);
                Hide();
                break;
            }
        }
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
            using (Graphics g = Graphics.FromImage(buffer))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                _lastHits = Draw(g, new RectangleF(0, 0, w, h));
            }
            using Graphics screenGraphics = Graphics.FromHdc(hdc);
            screenGraphics.DrawImageUnscaled(buffer, 0, 0);
        }

        PInvoke.EndPaint(_hwnd, &ps);
    }

    private List<(LauncherEntry Entry, RectangleF Bounds)> Draw(Graphics g, RectangleF clientBounds)
    {
        const float padding = 8f;
        var hits = new List<(LauncherEntry, RectangleF)>();

        using (var background = new SolidBrush(_theme.Background))
            g.FillRectangle(background, clientBounds);

        using var foregroundBrush = new SolidBrush(_theme.Foreground);
        using var selectedBackgroundBrush = new SolidBrush(_theme.ActiveTag);
        using var selectedForegroundBrush = new SolidBrush(_theme.Background);
        var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.None };

        float x = padding;
        float queryWidth = g.MeasureString(_query, _font, PointF.Empty, format).Width;
        g.DrawString(_query, _font, foregroundBrush, new PointF(x, clientBounds.Height / 2), format);
        x += queryWidth;

        // A static caret bar right after the typed text -- no blink timer, kept simple like the
        // rest of this window's input handling.
        float caretHeight = _font.GetHeight(g);
        g.FillRectangle(foregroundBrush, x, (clientBounds.Height - caretHeight) / 2, 2f, caretHeight);
        x += 4f + padding;

        for (int i = 0; i < _filtered.Count && x < clientBounds.Width - padding; i++)
        {
            LauncherEntry entry = _filtered[i];
            float textWidth = g.MeasureString(entry.DisplayName, _font, PointF.Empty, format).Width;
            float slotWidth = textWidth + padding;
            var bounds = new RectangleF(x, 0, slotWidth, clientBounds.Height);

            bool selected = i == _selectedIndex;
            if (selected)
                g.FillRectangle(selectedBackgroundBrush, bounds);
            g.DrawString(entry.DisplayName, _font, selected ? selectedForegroundBrush : foregroundBrush,
                new PointF(x + padding / 2, clientBounds.Height / 2), format);

            hits.Add((entry, bounds));
            x += slotWidth + padding;
        }

        return hits;
    }

    public void Dispose()
    {
        Instances.Remove((nint)_hwnd.Value);
        PInvoke.DestroyWindow(_hwnd);
        _font.Dispose();
    }
}
