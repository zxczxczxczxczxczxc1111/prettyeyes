using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.App.Controls;

/// <summary>
/// Captures a key combination while focused. A bare modifier is not a hotkey,
/// and Escape belongs to the window, so both are ignored.
/// </summary>
public sealed class HotkeyBox : Button
{
    private HotkeyDefinition _value = HotkeyDefinition.Default;

    public HotkeyBox()
    {
        Focusable = true;
        Content = Describe(_value);
    }

    public event EventHandler<HotkeyDefinition>? HotkeyChanged;

    public HotkeyDefinition Value
    {
        get => _value;
        set
        {
            _value = value;
            Content = Describe(value);
        }
    }

    public static string Describe(HotkeyDefinition hotkey)
    {
        var parts = new List<string>();

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(hotkey.VirtualKey));

        return string.Join(" + ", parts);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);

        // The field is a button until it is focused; then it is a prompt.
        Content = "нажми сочетание";
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Content = Describe(_value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Escape closes the window, and a bare Tab moves the focus on; Tab with
        // a modifier is nobody's, so it can be bound.
        if (e.Key is Key.Escape || (e.Key is Key.Tab && e.KeyModifiers == KeyModifiers.None))
        {
            base.OnKeyDown(e);
            return;
        }

        if (IsModifier(e.Key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = HotkeyModifiers.None;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        var virtualKey = ToVirtualKey(e.Key);

        if (virtualKey is null)
        {
            // Avalonia has no public Key-to-VK mapping, so the table below is
            // ours; anything outside it has no Win32 key to register.
            e.Handled = true;
            return;
        }

        // A bare letter or digit would eat that key everywhere in the system,
        // so a combination without modifiers is only allowed for keys nobody
        // types text with.
        if (modifiers == HotkeyModifiers.None && !StandsAlone(e.Key))
        {
            e.Handled = true;
            return;
        }

        Value = new HotkeyDefinition(modifiers, virtualKey.Value);
        HotkeyChanged?.Invoke(this, Value);
        e.Handled = true;
    }

    /// <summary>
    /// Win32 virtual-key codes for everything a keyboard can send. The values
    /// are the VK_* constants; Avalonia's Key enum follows WPF's order, which
    /// is not the same thing.
    /// </summary>
    private static uint? ToVirtualKey(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => (uint)(0x30 + (key - Key.D0)),
        >= Key.A and <= Key.Z => (uint)(0x41 + (key - Key.A)),
        >= Key.F1 and <= Key.F24 => (uint)(0x70 + (key - Key.F1)),
        >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (key - Key.NumPad0)),

        Key.Multiply => 0x6A,
        Key.Add => 0x6B,
        Key.Separator => 0x6C,
        Key.Subtract => 0x6D,
        Key.Decimal => 0x6E,
        Key.Divide => 0x6F,

        Key.Back => 0x08,
        Key.Tab => 0x09,
        Key.Clear => 0x0C,
        Key.Return => 0x0D,
        Key.Pause => 0x13,
        Key.Capital => 0x14,
        Key.Space => 0x20,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Select => 0x29,
        Key.Print => 0x2A,
        Key.Execute => 0x2B,
        Key.PrintScreen => 0x2C,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Help => 0x2F,
        Key.Apps => 0x5D,
        Key.NumLock => 0x90,
        Key.Scroll => 0x91,

        Key.OemSemicolon => 0xBA,
        Key.OemPlus => 0xBB,
        Key.OemComma => 0xBC,
        Key.OemMinus => 0xBD,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemTilde => 0xC0,
        Key.OemOpenBrackets => 0xDB,
        Key.OemPipe => 0xDC,
        Key.OemCloseBrackets => 0xDD,
        Key.OemQuotes => 0xDE,
        Key.Oem8 => 0xDF,
        Key.OemBackslash => 0xE2,

        Key.BrowserBack => 0xA6,
        Key.BrowserForward => 0xA7,
        Key.BrowserRefresh => 0xA8,
        Key.BrowserStop => 0xA9,
        Key.BrowserSearch => 0xAA,
        Key.BrowserFavorites => 0xAB,
        Key.BrowserHome => 0xAC,
        Key.VolumeMute => 0xAD,
        Key.VolumeDown => 0xAE,
        Key.VolumeUp => 0xAF,
        Key.MediaNextTrack => 0xB0,
        Key.MediaPreviousTrack => 0xB1,
        Key.MediaStop => 0xB2,
        Key.MediaPlayPause => 0xB3,

        _ => null,
    };

    /// <summary>
    /// Keys that mean nothing while typing, so binding them bare costs the user
    /// nothing. Everything else - letters, digits, punctuation, Space, Enter -
    /// needs a modifier, otherwise the key disappears from the whole system.
    /// </summary>
    private static bool StandsAlone(Key key) => key
        is Key.PrintScreen or Key.Print or Key.Pause or Key.Scroll or Key.Insert
        or Key.Help or Key.Apps or Key.BrowserBack or Key.BrowserForward
        or Key.BrowserRefresh or Key.BrowserStop or Key.BrowserSearch
        or Key.BrowserFavorites or Key.BrowserHome or Key.VolumeMute
        or Key.VolumeDown or Key.VolumeUp or Key.MediaNextTrack
        or Key.MediaPreviousTrack or Key.MediaStop or Key.MediaPlayPause
        || key is >= Key.F1 and <= Key.F24;

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;

    /// <summary>Short labels, the way a keycap reads.</summary>
    private static string KeyName(uint virtualKey) => virtualKey switch
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
