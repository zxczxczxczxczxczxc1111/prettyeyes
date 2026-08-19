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
