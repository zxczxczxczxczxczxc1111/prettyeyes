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

    [Fact]
    public void Settings_from_version_1_0_1_load_and_get_the_new_fields_filled_in()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        // Exactly what 1.0.1 wrote: three fields, no schema version.
        File.WriteAllText(path, """
            {
              "Hotkey": { "Modifiers": 1, "VirtualKey": 71 },
              "FullScreenHotkey": { "Modifiers": 6, "VirtualKey": 51 },
              "Autostart": false
            }
            """);

        var settings = new JsonSettingsStore(path).Load();

        Assert.Equal(new HotkeyDefinition(HotkeyModifiers.Alt, 0x47), settings.Hotkey);
        Assert.Equal(AppSettings.CurrentSchema, settings.SchemaVersion);
    }

    [Fact]
    public void A_failed_save_leaves_the_previous_file_intact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);

        store.Save(AppSettings.Default);
        var before = File.ReadAllText(path);

        // Held open by somebody else: the move cannot replace it.
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var saved = store.Save(AppSettings.Default with { Autostart = true });

            Assert.False(saved);
        }

        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void A_successful_save_leaves_no_temporary_file_behind()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        Assert.True(new JsonSettingsStore(path).Save(AppSettings.Default));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void A_file_older_than_the_magnifier_gets_it_switched_on()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        // Schema 1: no ShowMagnifier property at all.
        File.WriteAllText(path, """
            {
              "Hotkey": { "Modifiers": 1, "VirtualKey": 71 },
              "FullScreenHotkey": { "Modifiers": 6, "VirtualKey": 51 },
              "Autostart": false,
              "SchemaVersion": 1
            }
            """);

        var settings = new JsonSettingsStore(path).Load();

        Assert.True(settings.ShowMagnifier);
        Assert.Equal(AppSettings.CurrentSchema, settings.SchemaVersion);
    }

    [Fact]
    public void A_magnifier_switched_off_on_purpose_stays_off()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);

        store.Save(AppSettings.Default with { ShowMagnifier = false });

        Assert.False(store.Load().ShowMagnifier);
    }

    [Fact]
    public void A_file_older_than_the_pixel_grid_gets_it_switched_on()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        // Schema 2: the magnifier existed, its grid did not.
        File.WriteAllText(path, """
            {
              "Hotkey": { "Modifiers": 1, "VirtualKey": 71 },
              "FullScreenHotkey": { "Modifiers": 6, "VirtualKey": 51 },
              "Autostart": false,
              "ShowMagnifier": false,
              "SchemaVersion": 2
            }
            """);

        var settings = new JsonSettingsStore(path).Load();

        Assert.False(settings.ShowMagnifier);
        Assert.True(settings.MagnifierGrid);
    }

    [Fact]
    public void A_grid_switched_off_on_purpose_stays_off()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);

        store.Save(AppSettings.Default with { MagnifierGrid = false });

        Assert.False(store.Load().MagnifierGrid);
        Assert.True(store.Load().ShowMagnifier);
    }
}
