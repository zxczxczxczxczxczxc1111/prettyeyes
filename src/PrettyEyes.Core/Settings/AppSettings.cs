using PrettyEyes.Core.Platform;

namespace PrettyEyes.Core.Settings;

public sealed record AppSettings(
    HotkeyDefinition Hotkey,
    HotkeyDefinition FullScreenHotkey,
    bool Autostart)
{
    public static AppSettings Default =>
        new(HotkeyDefinition.Default, HotkeyDefinition.DefaultFullScreen, false);
}
