using Windows.Win32.Foundation;

namespace Wtile.Core;

internal sealed class ManagedWindow(HWND handle)
{
    public HWND Handle { get; } = handle;
    public string Title { get; set; } = "";
    public string ClassName { get; set; } = "";
    public bool IsMinimized { get; set; }
    public bool IsFloating { get; set; }
    public bool IsPinned { get; set; }
    public int TagIndex { get; set; }
    public int MonitorIndex { get; set; }

    /// <summary>GWL_STYLE as it was when this window was first tracked, before any titlebar
    /// hiding -- see WindowInspector.SetTitlebarHidden, which restores exactly this.</summary>
    public int OriginalStyle { get; set; }
}
