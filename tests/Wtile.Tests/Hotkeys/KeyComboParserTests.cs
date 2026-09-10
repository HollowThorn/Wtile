using Wtile.Hotkeys;

namespace Wtile.Tests.Hotkeys;

public class KeyComboParserTests
{
    [Theory]
    [InlineData("Alt+J", HotkeyModifiers.Alt, (uint)'J')]
    [InlineData("Alt+Shift+Return", HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x0Du)]
    [InlineData("Alt+1", HotkeyModifiers.Alt, (uint)'1')]
    [InlineData("Ctrl+Alt+Shift+Win+Escape", HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, 0x1Bu)]
    [InlineData("alt+j", HotkeyModifiers.Alt, (uint)'J')] // case-insensitive
    [InlineData("Alt+F5", HotkeyModifiers.Alt, 0x74u)]
    [InlineData("Alt+Space", HotkeyModifiers.Alt, 0x20u)]
    [InlineData("Win+`", HotkeyModifiers.Win, 0xC0u)]
    [InlineData("Win+Grave", HotkeyModifiers.Win, 0xC0u)]
    [InlineData("Win+-", HotkeyModifiers.Win, 0xBDu)]
    [InlineData("Win+=", HotkeyModifiers.Win, 0xBBu)]
    [InlineData("Win+Shift+=", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0xBBu)] // "+" is Shift+"="
    public void Parse_ValidCombos_ProducesExpectedResult(string text, HotkeyModifiers modifiers, uint vk)
    {
        KeyCombo combo = KeyComboParser.Parse(text);
        Assert.Equal(modifiers, combo.Modifiers);
        Assert.Equal(vk, combo.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Alt")] // modifier only, no key
    [InlineData("Alt+J+K")] // two non-modifier tokens
    [InlineData("Alt+NotAKey")]
    [InlineData("Alt+F99")]
    public void TryParse_InvalidCombos_ReturnsFalse(string text)
    {
        Assert.False(KeyComboParser.TryParse(text, out _));
    }

    [Fact]
    public void Parse_InvalidCombo_Throws()
    {
        Assert.Throws<FormatException>(() => KeyComboParser.Parse("Alt"));
    }
}
