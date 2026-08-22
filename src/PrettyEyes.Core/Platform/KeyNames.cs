namespace PrettyEyes.Core.Platform;

/// <summary>
/// What a virtual key is called on a keycap.
///
/// In Core rather than next to the field that shows it: the table is the one
/// thing here that quietly rots - a key added to the mapping and forgotten
/// here shows up as "0x2E" in the settings - and Core is the half a test can
/// see.
/// </summary>
public static class KeyNames
{
    /// <summary>Short labels, the way a keycap reads.</summary>
    public static string Describe(uint virtualKey) => virtualKey switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0C => "Clear",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "CapsLock",
        0x20 => "Space",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x29 => "Select",
        0x2A => "Print",
        0x2B => "Execute",
        0x2C => "PrtScn",
        0x2D => "Insert",
        0x2E => "Delete",
        0x2F => "Help",
        0x5D => "Menu",
        0x90 => "NumLock",
        0x91 => "ScrollLock",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6C => "Num Sep",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0xA6 => "Back",
        0xA7 => "Forward",
        0xA8 => "Refresh",
        0xA9 => "Stop",
        0xAA => "Search",
        0xAB => "Favorites",
        0xAC => "Browser",
        0xAD => "Mute",
        0xAE => "Vol -",
        0xAF => "Vol +",
        0xB0 => "Next",
        0xB1 => "Prev",
        0xB2 => "Stop",
        0xB3 => "Play",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        0xDF => "Oem8",
        0xE2 => "\\",
        >= 0x30 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x60 and <= 0x69 => $"Num {virtualKey - 0x60}",
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
        _ => $"0x{virtualKey:X2}",
    };
}
