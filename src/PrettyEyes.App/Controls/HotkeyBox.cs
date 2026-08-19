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
        if (e.Key is Key.Escape or Key.Tab)
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

        // PrintScreen is the one useful key that carries no modifier.
        if (modifiers == HotkeyModifiers.None && e.Key != Key.PrintScreen)
        {
            e.Handled = true;
            return;
        }

        var virtualKey = ToVirtualKey(e.Key);

        if (virtualKey is null)
        {
            // Avalonia has no Key-to-VK mapping in public API, so only the keys
            // worth binding are accepted and the rest is ignored on purpose.
            e.Handled = true;
            return;
        }

        Value = new HotkeyDefinition(modifiers, virtualKey.Value);
        HotkeyChanged?.Invoke(this, Value);
        e.Handled = true;
    }

    private static uint? ToVirtualKey(Key key) => key switch
    {
        Key.PrintScreen => 0x2C,
        >= Key.D0 and <= Key.D9 => (uint)(0x30 + (key - Key.D0)),
        >= Key.A and <= Key.Z => (uint)(0x41 + (key - Key.A)),
        >= Key.F1 and <= Key.F12 => (uint)(0x70 + (key - Key.F1)),
        _ => null,
    };

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;

    private static string KeyName(uint virtualKey) => virtualKey switch
    {
        0x2C => "PrtScn",
        >= 0x30 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x7B => $"F{virtualKey - 0x6F}",
        _ => $"0x{virtualKey:X2}",
    };
}
