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

    /// <summary>
    /// Ctrl+Shift+4. Not PrtScn: on Windows 11 the Snipping Tool grabs that key
    /// before any application sees it, and the default has to work out of the box.
    /// </summary>
    public static HotkeyDefinition Default =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x34);

    /// <summary>
    /// Ctrl+Shift+3 for the whole-monitor shot. Neighbour of the region default
    /// so the two are remembered as a pair.
    /// </summary>
    public static HotkeyDefinition DefaultFullScreen =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x33);
}
