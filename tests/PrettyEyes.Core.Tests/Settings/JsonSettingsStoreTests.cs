using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;
using Xunit;

namespace PrettyEyes.Core.Tests.Settings;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Missing_file_yields_defaults()
    {
        var store = new JsonSettingsStore(Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json"));

        var settings = store.Load();

        Assert.Equal(HotkeyDefinition.Default, settings.Hotkey);
        Assert.False(settings.Autostart);
    }

    [Fact]
    public void Corrupt_file_yields_defaults_instead_of_throwing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        File.WriteAllText(path, "{ this is not json");

        var settings = new JsonSettingsStore(path).Load();

        Assert.Equal(HotkeyDefinition.Default, settings.Hotkey);
    }

    [Fact]
    public void Settings_saved_before_the_second_hotkey_existed_load_with_the_default()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        File.WriteAllText(path, """{ "Hotkey": { "Modifiers": 6, "VirtualKey": 52 }, "Autostart": false }""");

        var settings = new JsonSettingsStore(path).Load();

        Assert.Equal(HotkeyDefinition.Default, settings.Hotkey);
        Assert.Equal(HotkeyDefinition.DefaultFullScreen, settings.FullScreenHotkey);
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);
        var saved = new AppSettings(
            new HotkeyDefinition(HotkeyModifiers.Alt, 0x41),
            new HotkeyDefinition(HotkeyModifiers.Alt, 0x42),
            true);

        store.Save(saved);

        Assert.Equal(saved, store.Load());
    }
}
