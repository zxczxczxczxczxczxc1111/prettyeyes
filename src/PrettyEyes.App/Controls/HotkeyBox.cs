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

    /// <summary>
    /// What a bare key costs the rest of the system, or null when nothing.
    /// A warning, not a question: the choice has already been made and taken.
    /// </summary>
    public event EventHandler<string?>? Warned;

    /// <summary>Why a key could not be taken at all.</summary>
    public event EventHandler<string>? Refused;

    /// <summary>The key a key-down already bound, so its release is ignored.</summary>
    private Key _bound = Key.None;

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

    /// <summary>
    /// Why the system refused a combination, in the words that lead somewhere.
    /// PrtScn gets its own line because it is almost never another program:
    /// Windows 11 hands the key to the Snipping Tool by default, and nobody
    /// finds that switch by guessing.
    /// </summary>
    public static string Busy(HotkeyDefinition hotkey) =>
        hotkey.VirtualKey == VkSnapshot
            ? "PrtScn уже кем-то занят. Чаще всего это «Ножницы»: Параметры, "
              + "Специальные возможности, Клавиатура, выключи кнопку Print screen. "
              + "Иначе клавишу держит другой скриншотер."
            : $"Комбинация {Describe(hotkey)} занята другой программой.";

    /// <summary>VK_SNAPSHOT, the one key with its own folklore.</summary>
    private const uint VkSnapshot = 0x2C;

    public static string Describe(HotkeyDefinition hotkey)
    {
        if (!hotkey.Assigned)
        {
            return "не назначена";
        }

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

        parts.Add(KeyNames.Describe(hotkey.VirtualKey));

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

        if (Capture(e))
        {
            _bound = e.Key;
        }
    }

    /// <summary>
    /// PrtScn is the one key Windows never sends a key-down for: the driver
    /// delivers the release and nothing else, which is why holding it does not
    /// repeat. A field that only listens on key-down therefore cannot be given
    /// that key at all - it looks like the field is broken, and it was.
    ///
    /// Only that key, and only when the key-down never came: everything else is
    /// already bound by the time it is released, and binding it twice would
    /// catch the second half of the gesture with the modifiers already gone.
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.PrintScreen && _bound != Key.PrintScreen)
        {
            Capture(e);
            _bound = Key.None;
            return;
        }

        _bound = Key.None;
        base.OnKeyUp(e);
    }

    /// <summary>True when the combination was taken; false when it was refused.</summary>
    private bool Capture(KeyEventArgs e)
    {
        if (IsModifier(e.Key))
        {
            e.Handled = true;
            return false;
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
            // ours; anything outside it has no Win32 key to register. This is
            // now the only reason a key is refused here.
            e.Handled = true;
            Refused?.Invoke(this, "Такую клавишу система не отдаёт.");

            return false;
        }

        Value = new HotkeyDefinition(modifiers, virtualKey.Value);
        HotkeyChanged?.Invoke(this, Value);
        Warned?.Invoke(this, Cost(modifiers, e.Key));
        e.Handled = true;

        return true;
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
    /// Keys nobody types with. Binding one of these bare costs nothing, so
    /// nothing is said about it.
    ///
    /// This used to be the list of what was <b>allowed</b> bare, and everything
    /// else was refused outright. It is now the list of what passes in silence:
    /// any key at all can be assigned, and the ones that cost something say so
    /// instead of being forbidden. Which key is worth having is the user's
    /// call, not ours.
    /// </summary>
    private static bool Quiet(Key key) => key
        is Key.PrintScreen or Key.Print or Key.Pause or Key.Scroll or Key.Insert
        or Key.Help or Key.Apps or Key.BrowserBack or Key.BrowserForward
        or Key.BrowserRefresh or Key.BrowserStop or Key.BrowserSearch
        or Key.BrowserFavorites or Key.BrowserHome or Key.VolumeMute
        or Key.VolumeDown or Key.VolumeUp or Key.MediaNextTrack
        or Key.MediaPreviousTrack or Key.MediaStop or Key.MediaPlayPause
        || key is >= Key.F1 and <= Key.F24;

    /// <summary>
    /// What taking this combination costs everywhere else. A global hotkey is
    /// taken from the whole system, so a bare letter stops typing that letter.
    /// With a modifier nothing is lost: Ctrl+P was never a character.
    /// </summary>
    private static string? Cost(HotkeyModifiers modifiers, Key key)
    {
        if (modifiers != HotkeyModifiers.None || Quiet(key))
        {
            return null;
        }

        var name = KeyNames.Describe(ToVirtualKey(key) ?? 0);

        return Types(key)
            ? $"Клавиша {name} нужна для набора: теперь она не будет печататься нигде"
            : $"Клавиша {name} перестанет работать в других программах";
    }

    /// <summary>
    /// Keys that put a character on the screen. Losing one of these is a
    /// different kind of loss than losing Delete: not one function, but the
    /// ability to write.
    /// </summary>
    private static bool Types(Key key) => key
        is >= Key.D0 and <= Key.D9
        or >= Key.A and <= Key.Z
        or >= Key.NumPad0 and <= Key.NumPad9
        or Key.Space or Key.Return or Key.Tab or Key.Back
        or Key.OemSemicolon or Key.OemPlus or Key.OemComma or Key.OemMinus
        or Key.OemPeriod or Key.OemQuestion or Key.OemTilde or Key.OemOpenBrackets
        or Key.OemPipe or Key.OemCloseBrackets or Key.OemQuotes or Key.Oem8
        or Key.OemBackslash or Key.Multiply or Key.Add or Key.Subtract
        or Key.Decimal or Key.Divide or Key.Separator;

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;

}
