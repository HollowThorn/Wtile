using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wtile.Core;

// Monitor handles go stale on sleep/wake, resolution changes and display reconfiguration, and
// tiling skips a monitor whose handle no longer resolves. Windows broadcasts these changes to
// top-level windows only, hence a never-shown one.
internal sealed unsafe class DisplayChangeWatcher : IDisposable
{
    private const string ClassName = "WtileDisplayWatcher";
    private const nuint SettleTimerId = 1;
    private const uint SettleDelayMs = 1000; // a single change fires several messages, and displays wake late
    private const uint RetryDelayMs = 3000;
    private const int MaxRetries = 3;

    private static readonly Dictionary<nint, DisplayChangeWatcher> Instances = [];
    private static bool _classRegistered;

    private readonly HWND _hwnd;
    private readonly Func<bool> _refreshDisplays; // returns whether every connected display reported usable geometry
    private int _retriesLeft;

    public DisplayChangeWatcher(Func<bool> refreshDisplays)
    {
        _refreshDisplays = refreshDisplays;
        EnsureClassRegistered();
        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, ClassName, "Wtile Display Watcher", WINDOW_STYLE.WS_POPUP,
            0, 0, 0, 0, HWND.Null, null, PInvoke.GetModuleHandle((string?)null), null);
        Instances[(nint)_hwnd.Value] = this;
    }

    private void ScheduleRefresh(uint delayMs) => PInvoke.SetTimer(_hwnd, SettleTimerId, delayMs, null);

    private void OnDisplayChanged(string reason)
    {
        Console.WriteLine($"[display] {reason}; refreshing monitors shortly.");
        _retriesLeft = MaxRetries;
        ScheduleRefresh(SettleDelayMs);
    }

    private void OnSettled()
    {
        PInvoke.KillTimer(_hwnd, SettleTimerId);
        if (_refreshDisplays())
            return;
        if (_retriesLeft-- > 0)
        {
            Console.WriteLine("[display] A display still reports no usable geometry; retrying.");
            ScheduleRefresh(RetryDelayMs);
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
        if (Instances.TryGetValue((nint)hwnd.Value, out DisplayChangeWatcher? self))
        {
            switch (msg)
            {
                case PInvoke.WM_DISPLAYCHANGE:
                    self.OnDisplayChanged("Display configuration changed");
                    break;
                case PInvoke.WM_POWERBROADCAST when wParam.Value is PInvoke.PBT_APMRESUMEAUTOMATIC or PInvoke.PBT_APMRESUMESUSPEND:
                    self.OnDisplayChanged("Resumed from sleep");
                    break;
                case PInvoke.WM_TIMER when wParam.Value == SettleTimerId:
                    self.OnSettled();
                    return new LRESULT(0);
            }
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        PInvoke.KillTimer(_hwnd, SettleTimerId);
        Instances.Remove((nint)_hwnd.Value);
        PInvoke.DestroyWindow(_hwnd);
    }
}
