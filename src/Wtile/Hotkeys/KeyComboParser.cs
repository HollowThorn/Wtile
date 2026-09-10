using System.Globalization;

namespace Wtile.Hotkeys;

/// <summary>
/// Parses config strings like <c>"Alt+Shift+Return"</c> or <c>"Alt+1"</c> into a
/// <see cref="KeyCombo"/> (modifier flags + Win32 virtual-key code).
/// </summary>
public static class KeyComboParser
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alt"] = HotkeyModifiers.Alt,
        ["ctrl"] = HotkeyModifiers.Control,
        ["control"] = HotkeyModifiers.Control,
        ["shift"] = HotkeyModifiers.Shift,
        ["win"] = HotkeyModifiers.Win,
        ["windows"] = HotkeyModifiers.Win,
        ["super"] = HotkeyModifiers.Win,
    };

    // A pragmatic subset of VK_* codes covering everything a WM config is likely to bind.
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backspace"] = 0x08,
        ["tab"] = 0x09,
        ["enter"] = 0x0D,
        ["return"] = 0x0D,
        ["escape"] = 0x1B,
        ["esc"] = 0x1B,
        ["space"] = 0x20,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["end"] = 0x23,
        ["home"] = 0x24,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["insert"] = 0x2D,
        ["delete"] = 0x2E,
        ["del"] = 0x2E,
        ["`"] = 0xC0, // VK_OEM_3, the backtick/grave/tilde key on a US keyboard layout
        ["grave"] = 0xC0,
        ["-"] = 0xBD, // VK_OEM_MINUS, the "-/_" key
        ["minus"] = 0xBD,
        ["="] = 0xBB, // VK_OEM_PLUS, the "=/+" key -- write "Win+Shift+=" for the "+" combo, same physical key
        ["equal"] = 0xBB,
        ["plus"] = 0xBB,
        [","] = 0xBC, // VK_OEM_COMMA
        ["comma"] = 0xBC,
        ["."] = 0xBE, // VK_OEM_PERIOD
        ["period"] = 0xBE,
    };

    public static KeyCombo Parse(string text)
    {
        if (!TryParse(text, out KeyCombo combo))
            throw new FormatException($"Invalid key combo: '{text}'.");
        return combo;
    }

    public static bool TryParse(string text, out KeyCombo combo)
    {
        combo = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var modifiers = HotkeyModifiers.None;
        uint? virtualKey = null;

        foreach (string part in parts)
        {
            if (ModifierNames.TryGetValue(part, out HotkeyModifiers modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (virtualKey is not null)
                return false; // more than one non-modifier token, e.g. "Alt+J+K"

            if (!TryParseKey(part, out uint vk))
                return false;
            virtualKey = vk;
        }

        if (virtualKey is null)
            return false;

        combo = new KeyCombo(modifiers, virtualKey.Value);
        return true;
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        if (NamedKeys.TryGetValue(token, out virtualKey))
            return true;

        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }
        }

        if (token.Length is 2 or 3 && (token[0] is 'F' or 'f') &&
            int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int fn) &&
            fn is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + (fn - 1)); // VK_F1 = 0x70, sequential through VK_F24
            return true;
        }

        virtualKey = 0;
        return false;
    }

    public static string Format(KeyCombo combo)
    {
        var pieces = new List<string>();
        if (combo.Modifiers.HasFlag(HotkeyModifiers.Control)) pieces.Add("Ctrl");
        if (combo.Modifiers.HasFlag(HotkeyModifiers.Alt)) pieces.Add("Alt");
        if (combo.Modifiers.HasFlag(HotkeyModifiers.Shift)) pieces.Add("Shift");
        if (combo.Modifiers.HasFlag(HotkeyModifiers.Win)) pieces.Add("Win");
        pieces.Add(DescribeKey(combo.VirtualKey));
        return string.Join("+", pieces);
    }

    private static string DescribeKey(uint vk)
    {
        foreach ((string name, uint code) in NamedKeys)
            if (code == vk)
                return char.ToUpperInvariant(name[0]) + name[1..];
        if (vk is >= 0x70 and <= 0x87)
            return $"F{vk - 0x70 + 1}";
        return ((char)vk).ToString();
    }
}
