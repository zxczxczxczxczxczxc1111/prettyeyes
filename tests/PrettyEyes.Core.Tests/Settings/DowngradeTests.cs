using System.Text.Json;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Settings;

public class DowngradeTests
{
    // No converters: this is 1.2.0's serializer, warts and all.
    private static JsonSerializerOptions Legacy => new() { WriteIndented = true };

    [Fact]
    public void A_file_from_1_3_is_read_by_1_2_with_settings_intact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        new JsonSettingsStore(path).Save(AppSettings.Default with
        {
            Autostart = true,
            // Marker, not Text: Text does not exist until phase 4, and this
            // test lives in phase 0. The Text-specific downgrade case is a step
            // inside task 10.
            Tools = new Dictionary<ToolKind, bool> { [ToolKind.Marker] = false },
        });

        var old = JsonSerializer.Deserialize<LegacyAppSettings>(File.ReadAllText(path), Legacy);

        Assert.NotNull(old);
        Assert.True(old!.Autostart);
    }

    [Fact]
    public void A_round_trip_down_and_back_up_keeps_autostart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");
        new JsonSettingsStore(path).Save(AppSettings.Default with
        {
            Autostart = true,
            // Without a dictionary there are no enum keys, and the test proves
            // nothing at all while looking green.
            Tools = new Dictionary<ToolKind, bool> { [ToolKind.Marker] = false },
        });

        // 1.2.0 reads, then saves back with names in the keys, and its Normalize
        // stamps SchemaVersion 11 on the way out.
        var old = JsonSerializer.Deserialize<LegacyAppSettings>(File.ReadAllText(path), Legacy);

        // The real 1.2.0 stamps its own schema in Normalize before saving. Skip
        // that and the test models a kinder 1.2 than the one people run.
        File.WriteAllText(path, JsonSerializer.Serialize(old! with { SchemaVersion = 11 }, Legacy));

        Assert.True(new JsonSettingsStore(path).Load().Autostart);
    }
}
