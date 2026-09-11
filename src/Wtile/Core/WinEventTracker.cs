using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Wtile.Core;

/// <summary>
/// Wraps <c>SetWinEventHook</c> (OUT_OF_CONTEXT) so <see cref="WindowManager"/> hears about
/// window show/hide/destroy/minimize without polling. Delivery requires a running
/// GetMessage/DispatchMessage loop on the thread that constructs this tracker.
/// </summary>
internal sealed unsafe class WinEventTracker : IDisposable
{
    private HWINEVENTHOOK _hook;

    public WinEventTracker()
    {
        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_MIN,
            PInvoke.EVENT_OBJECT_END,
            HMODULE.Null,
            &WinEventProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void WinEventProc(
        HWINEVENTHOOK hWinEventHook, uint eventId, HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        WindowManager? target = WindowManager.Current;
        if (target is null || hwnd.IsNull)
            return;

        // idObject == OBJID_WINDOW (0) && idChild == CHILDID_SELF (0): the window itself, not
        // one of its child controls -- otherwise this callback fires for every UI element.
        if (idObject != 0 || idChild != 0)
            return;

        switch (eventId)
        {
            case PInvoke.EVENT_OBJECT_SHOW:
                target.OnWindowShown(hwnd);
                break;
            case PInvoke.EVENT_OBJECT_UNCLOAKED:
                // Some apps' main window becomes visible via a DWM "uncloak" rather than a fresh
                // SW_SHOW (observed with Firefox) -- EVENT_OBJECT_SHOW never fires for that
                // transition. This is the correctly-timed fix (see OnForegroundChanged for the
                // looser, focus-triggered fallback this complements -- that one can race ahead of
                // the actual uncloak and miss a still-cloaked window).
                target.OnWindowShown(hwnd);
                // The immediate arrange above can still land at the wrong size/position -- right
                // at uncloak, DWM's extended-frame-bounds for this window (used to compensate for
                // its invisible resize border) or the app's own post-show geometry may not have
                // settled yet. A short follow-up re-arrange self-corrects it (see
                // WindowManager.ScheduleRearrange) instead of leaving it wrong until something
                // else happens to trigger another arrange.
                target.ScheduleRearrange(150);
                break;
            case PInvoke.EVENT_OBJECT_HIDE:
                target.OnWindowHidden(hwnd);
                break;
            case PInvoke.EVENT_OBJECT_CLOAKED:
                // The reverse of EVENT_OBJECT_UNCLOAKED above: DWM hid this window without a
                // Win32-level hide (virtual-desktop switch, or a shell flyout dismissed without
                // being destroyed) -- see WindowManager.OnWindowCloaked.
                target.OnWindowCloaked(hwnd);
                break;
            case PInvoke.EVENT_OBJECT_DESTROY:
                target.OnWindowDestroyed(hwnd);
                break;
            case PInvoke.EVENT_SYSTEM_MOVESIZESTART:
                target.OnWindowMoveStarted(hwnd);
                break;
            case PInvoke.EVENT_SYSTEM_MOVESIZEEND:
                // Fires once, when the user releases a drag/resize (the SC_MOVE/SC_SIZE modal
                // loop exits) -- deliberately not EVENT_OBJECT_LOCATIONCHANGE, which fires
                // continuously during the drag but was unreliably delivered cross-process via this
                // out-of-context hook; this is a single, cheap, always-delivered system event, so
                // the focus border just snaps to the window's final position once it's let go.
                target.OnWindowMoved(hwnd);
                break;
            case PInvoke.EVENT_SYSTEM_MINIMIZESTART:
                target.OnMinimizeChanged(hwnd, minimized: true);
                break;
            case PInvoke.EVENT_SYSTEM_MINIMIZEEND:
                target.OnMinimizeChanged(hwnd, minimized: false);
                break;
            case PInvoke.EVENT_SYSTEM_FOREGROUND:
                target.OnForegroundChanged(hwnd);
                break;
            case PInvoke.EVENT_OBJECT_NAMECHANGE:
                target.OnTitleChanged(hwnd);
                break;
        }
    }

    public void Dispose()
    {
        if (_hook.IsNull)
            return;
        PInvoke.UnhookWinEvent(_hook);
        _hook = default;
    }
}
