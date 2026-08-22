using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
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
    public void Grain_and_light_are_on_for_a_file_written_before_they_existed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        File.WriteAllText(path, """
            { "Export": { "Enabled": true, "Padding": 48, "Background": 1, "CornerRadius": 16, "Shadow": true } }
            """);

        var export = new JsonSettingsStore(path).Load().Export;

        Assert.NotNull(export);
        Assert.True(export!.Grain);
        Assert.True(export.Sheen);
    }

    [Fact]
    public void The_stored_background_keeps_its_meaning_after_a_new_one_was_added()
    {
        // Backgrounds are numbers in the file. Anything but appending to the
        // enum turns somebody's white into transparent without a word.
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        File.WriteAllText(path, """
            { "Export": { "Enabled": true, "Padding": 48, "Background": 2, "CornerRadius": 16, "Shadow": true } }
            """);

        var export = new JsonSettingsStore(path).Load().Export;

        Assert.Equal(ExportBackground.White, export!.Background);
    }

    [Fact]
    public void Grain_stays_off_the_transparent_background()
    {
        var style = new ExportStyle(true, 48, ExportBackground.Transparent, 16, true);

        Assert.True(style.Grain);
        Assert.False(style.GrainAllowed);
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

        // What was written down is kept, whatever the current default happens
        // to be: this file was saved by a build whose default was Ctrl+Shift+4,
        // and a later build changing its mind must not move somebody's key.
        Assert.Equal(new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x34), settings.Hotkey);

        // The one that did not exist yet is the one that gets a default.
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
        var loaded = store.Load();

        // Field by field, not record against record: AppSettings holds a
        // dictionary of tool styles, and a dictionary compares by reference.
        Assert.Equal(saved.Hotkey, loaded.Hotkey);
        Assert.Equal(saved.FullScreenHotkey, loaded.FullScreenHotkey);
        Assert.Equal(saved.Autostart, loaded.Autostart);
        Assert.Equal(saved.ShowMagnifier, loaded.ShowMagnifier);
        Assert.Equal(saved.MagnifierGrid, loaded.MagnifierGrid);
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

    [Fact]
    public void Tool_styles_survive_a_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);

        store.Save(AppSettings.Default with
        {
            ToolStyles = new Dictionary<ToolKind, ToolStyle>
            {
                [ToolKind.Arrow] = new(Palette.Blue, StrokeSize.Large),
            },
        });

        var loaded = new ToolStyles(store.Load().ToolStyles!);

        Assert.Equal(Palette.Blue, loaded.For(ToolKind.Arrow).Color);
        Assert.Equal(StrokeSize.Large, loaded.For(ToolKind.Arrow).Size);
        Assert.Equal(ToolStyle.Default, loaded.For(ToolKind.Rectangle));
    }

    [Fact]
    public void A_file_without_tool_styles_loads_as_no_styles_rather_than_null()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        File.WriteAllText(path, """
            {
              "Hotkey": { "Modifiers": 1, "VirtualKey": 71 },
              "FullScreenHotkey": { "Modifiers": 6, "VirtualKey": 51 },
              "Autostart": false,
              "SchemaVersion": 3
            }
            """);

        var settings = new JsonSettingsStore(path).Load();

        Assert.NotNull(settings.ToolStyles);
        Assert.Empty(settings.ToolStyles);
    }

    [Fact]
    public void A_file_older_than_the_update_check_gets_it_switched_on()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

        // Schema 7: everything up to the export frame, no update check.
        File.WriteAllText(path, """
            {
              "Hotkey": { "Modifiers": 1, "VirtualKey": 71 },
              "FullScreenHotkey": { "Modifiers": 6, "VirtualKey": 51 },
              "Autostart": false,
              "SchemaVersion": 7
            }
            """);

        var settings = new JsonSettingsStore(path).Load();

        Assert.True(settings.CheckUpdates);
        Assert.Equal(AppSettings.CurrentSchema, settings.SchemaVersion);
    }

    [Fact]
    public void An_update_check_switched_off_on_purpose_stays_off()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        var store = new JsonSettingsStore(path);

        store.Save(AppSettings.Default with { CheckUpdates = false });

        Assert.False(store.Load().CheckUpdates);
    }

    [Fact]
    public void A_schema_11_file_reads_and_gets_the_1_3_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        File.WriteAllText(path, """{ "Autostart": true, "SchemaVersion": 11 }""");

        var settings = new JsonSettingsStore(path).Load();

        Assert.True(settings.Autostart);
        Assert.Null(settings.DefaultTool);          // "not chosen" is the default
        Assert.True(settings.PinButtonShown);       // the headline feature is visible
        Assert.False(settings.HidePinnedOnCapture); // screen sharing beats screenshots
        Assert.Null(settings.PinHotkey);
        // Not asserting SchemaVersion here: Normalize assigns it unconditionally,
        // so the check would pass no matter what the rest of this does.
        Assert.Equal(12, AppSettings.CurrentSchema);
    }
}
