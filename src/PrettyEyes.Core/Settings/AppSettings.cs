using PrettyEyes.Core.Platform;

namespace PrettyEyes.Core.Settings;

public sealed record AppSettings(HotkeyDefinition Hotkey, bool Autostart)
{
    public static AppSettings Default => new(HotkeyDefinition.Default, false);
}
