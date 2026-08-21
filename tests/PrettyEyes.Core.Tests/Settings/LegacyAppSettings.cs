using PrettyEyes.Core.Platform;

namespace PrettyEyes.Core.Tests.Settings;

/// <summary>
/// What 1.2.0 knows about. Copied by hand and frozen: the moment this file
/// imports the real ToolKind, the downgrade test starts asking 1.3 whether 1.3
/// can read 1.3, and answers yes forever.
/// </summary>
internal enum LegacyToolKind
{
    Blur, Arrow, Line, Rectangle, Pencil, Marker, Emoji,
}

internal sealed record LegacyAppSettings(
    HotkeyDefinition Hotkey,
    HotkeyDefinition FullScreenHotkey,
    bool Autostart,
    Dictionary<LegacyToolKind, bool>? Tools = null,
    int SchemaVersion = 11);
