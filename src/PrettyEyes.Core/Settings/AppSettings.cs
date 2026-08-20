using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Tools;

namespace PrettyEyes.Core.Settings;

public sealed record AppSettings(
    HotkeyDefinition Hotkey,
    HotkeyDefinition FullScreenHotkey,
    bool Autostart,
    bool ShowMagnifier = true,
    bool MagnifierGrid = true,
    Dictionary<ToolKind, ToolStyle>? ToolStyles = null,
    string? Emoji = null,
    List<string>? RecentEmoji = null,
    int SchemaVersion = AppSettings.CurrentSchema)
{
    /// <summary>
    /// Raised whenever a released version adds a field. A file written by an
    /// older build is missing everything added since, and the reader has to
    /// know what to fill in rather than hand out nulls.
    /// </summary>
    public const int CurrentSchema = 5;

    public static AppSettings Default =>
        new(HotkeyDefinition.Default, HotkeyDefinition.DefaultFullScreen, false);
}
