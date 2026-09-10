namespace Wtile.Hotkeys;

/// <summary>
/// Values match the Win32 <c>RegisterHotKey</c> MOD_* constants exactly, so a parsed combo
/// can be passed straight through to the P/Invoke call with no translation.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary><see cref="VirtualKey"/> is a standard Win32 virtual-key code (VK_*).</summary>
public readonly record struct KeyCombo(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public override string ToString() => KeyComboParser.Format(this);
}
