using System.Text.Json.Serialization;

namespace PrettyEyes.Core.Platform;

/// <summary>Modifier flags matching the Win32 MOD_* values.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>
    /// Nothing is assigned. A real value rather than a null so a field can show
    /// it and a table can hold it; the registrar skips it, and two of these are
    /// never a conflict with each other.
    /// </summary>
    public static HotkeyDefinition None => new(HotkeyModifiers.None, 0);

    /// <summary>
    /// Whether there is anything here to register. Kept out of the file: it is
    /// derived from the key, and a settings file that carries both can be
    /// self-contradictory.
    /// </summary>
    [JsonIgnore]
    public bool Assigned => VirtualKey != 0;

    /// <summary>Print Screen.</summary>
    private const uint PrintScreen = 0x2C;

    /// <summary>
    /// Alt+PrtScn for the region, because that is the key everybody already
    /// presses when they want a screenshot. Bare PrtScn is not offered: on
    /// Windows 11 the Snipping Tool takes it before any application sees it,
    /// and a default that depends on a system setting is not a default.
    ///
    /// Alt+PrtScn is a system shortcut of its own - copy the active window to
    /// the clipboard - and Windows hands it over when it is asked for: checked
    /// with RegisterHotKey, which answers rather than guesses.
    /// </summary>
    public static HotkeyDefinition Default => new(HotkeyModifiers.Alt, PrintScreen);

    /// <summary>
    /// Ctrl+PrtScn for the whole monitor. Same key as the region shot, so the
    /// pair is remembered as one thing with two modifiers.
    /// </summary>
    public static HotkeyDefinition DefaultFullScreen => new(HotkeyModifiers.Control, PrintScreen);
}
