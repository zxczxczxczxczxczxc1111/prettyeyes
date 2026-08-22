using PrettyEyes.Core.Platform;
using Xunit;

namespace PrettyEyes.Core.Tests.Platform;

/// <summary>
/// The table that turns a virtual key into something a person recognises.
/// It is the piece most likely to rot: a key added to the field's own mapping
/// and forgotten here shows up in the settings as "0x2E".
/// </summary>
public class KeyNamesTests
{
    [Theory]
    [InlineData(0x08u, "Backspace")]
    [InlineData(0x0Du, "Enter")]
    [InlineData(0x20u, "Space")]
    [InlineData(0x2Cu, "PrtScn")]
    [InlineData(0x2Eu, "Delete")]
    [InlineData(0x24u, "Home")]
    [InlineData(0xBCu, ",")]
    public void Known_keys_read_like_a_keycap(uint key, string expected) =>
        Assert.Equal(expected, KeyNames.Describe(key));

    [Fact]
    public void Every_key_a_hotkey_can_use_has_a_name()
    {
        // The ranges the field maps: digits, letters, the F row, the numpad,
        // the navigation block, punctuation and the media keys. None of them
        // may fall through to the hex escape hatch.
        var keys = new List<uint>();

        for (var key = 0x30u; key <= 0x5Au; key++)
        {
            keys.Add(key);
        }

        for (var key = 0x60u; key <= 0x6Fu; key++)
        {
            keys.Add(key);
        }

        for (var key = 0x70u; key <= 0x87u; key++)
        {
            keys.Add(key);
        }

        keys.AddRange([0x08u, 0x09u, 0x0Cu, 0x0Du, 0x13u, 0x14u, 0x20u]);
        keys.AddRange([0x21u, 0x22u, 0x23u, 0x24u, 0x25u, 0x26u, 0x27u, 0x28u, 0x29u]);
        keys.AddRange([0x2Au, 0x2Bu, 0x2Cu, 0x2Du, 0x2Eu, 0x2Fu, 0x5Du, 0x90u, 0x91u]);
        keys.AddRange([0xA6u, 0xA7u, 0xA8u, 0xA9u, 0xAAu, 0xABu, 0xACu, 0xADu, 0xAEu, 0xAFu]);
        keys.AddRange([0xB0u, 0xB1u, 0xB2u, 0xB3u]);
        keys.AddRange([0xBAu, 0xBBu, 0xBCu, 0xBDu, 0xBEu, 0xBFu, 0xC0u]);
        keys.AddRange([0xDBu, 0xDCu, 0xDDu, 0xDEu, 0xDFu, 0xE2u]);

        var nameless = keys.Where(key => KeyNames.Describe(key).StartsWith("0x", StringComparison.Ordinal));

        Assert.Empty(nameless);
    }

    [Fact]
    public void An_unmapped_code_says_so_rather_than_pretending()
    {
        // 0x07 is not a key anybody has. Showing the code beats showing a
        // plausible name for something else.
        Assert.Equal("0x07", KeyNames.Describe(0x07));
    }
}
