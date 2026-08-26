using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Settings;

public class Schema13Tests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"pe-{Guid.NewGuid()}.json");

    /// <summary>
    /// A settings file exactly as the released build wrote it: a tool style
    /// with a preset and no width at all.
    ///
    /// Written as raw text rather than through Save, and that is the point.
    /// Save now rewrites the preset from the width on its way out, so a file
    /// produced that way would already have lost the very thing the migration
    /// reads. Only a build that predates Width leaves a file like this.
    /// </summary>
    private static string Legacy(ToolKind kind, uint colour, StrokeSize size)
    {
        var path = TempFile();

        File.WriteAllText(path, $$"""
            {
              "ToolStyles": { "{{(int)kind}}": { "Color": {{colour}}, "Size": {{(int)size}} } },
              "SchemaVersion": 12
            }
            """);

        return path;
    }

    [Fact]
    public void A_preset_becomes_the_number_of_pixels_it_used_to_draw()
    {
        var path = Legacy(ToolKind.Line, Palette.Blue, StrokeSize.Large);

        var styles = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Large drew five pixels, so five is what the number has to say.
        Assert.Equal(5, styles.For(ToolKind.Line).Width);
    }

    [Fact]
    public void The_marker_keeps_the_width_it_actually_had()
    {
        var path = Legacy(ToolKind.Marker, Palette.Yellow, StrokeSize.Medium);

        var styles = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Three on the card, twelve on the screen, because the annotation
        // multiplied it. The multiplier is gone, so the number takes its place.
        Assert.Equal(12, styles.For(ToolKind.Marker).Width);
    }

    [Fact]
    public void A_stored_arrow_switches_to_freehand_once()
    {
        var path = Legacy(ToolKind.Arrow, Palette.Green, StrokeSize.Small);

        var styles = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Nobody chose the straight arrow: the choice did not exist when this
        // file was written. Changing the default alone would never reach it.
        Assert.True(styles.For(ToolKind.Arrow).FreehandArrow);
        Assert.Equal(Palette.Green, styles.For(ToolKind.Arrow).Color);
    }

    [Fact]
    public void A_file_already_on_the_new_schema_is_left_alone()
    {
        var path = TempFile();

        new JsonSettingsStore(path).Save(AppSettings.Default with
        {
            ToolStyles = new Dictionary<ToolKind, ToolStyle>
            {
                [ToolKind.Arrow] = ToolStyle.Default.WithWidth(7) with { FreehandArrow = false },
            },
        });

        var styles = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Here the straight arrow IS a choice, and the migration must not reach
        // in and undo it on every launch.
        Assert.False(styles.For(ToolKind.Arrow).FreehandArrow);
        Assert.Equal(7, styles.For(ToolKind.Arrow).Width);
    }

    [Theory]
    [InlineData(ToolKind.Line, 2, 2)]
    [InlineData(ToolKind.Line, 3, 3)]
    [InlineData(ToolKind.Marker, 8, 8)]
    [InlineData(ToolKind.Marker, 12, 12)]
    [InlineData(ToolKind.Marker, 20, 20)]
    public void The_released_build_reads_back_the_width_that_was_chosen(
        ToolKind kind, int chosen, int expected)
    {
        var path = TempFile();

        new JsonSettingsStore(path).Save(AppSettings.Default with
        {
            ToolStyles = new Dictionary<ToolKind, ToolStyle>
            {
                [kind] = ToolStyle.DefaultFor(kind).WithWidth(chosen),
            },
        });

        var written = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Every width here is one the three old presets can express exactly, so
        // the round trip has to be exact. The marker is the whole reason this
        // test exists: its preset is multiplied by four by the build that reads
        // it, and a mirror computed without knowing the tool turned its 8 into
        // 20 and never gave it back.
        Assert.Equal(expected, DrawnByReleasedBuild(kind, written.For(kind).Size));
    }

    [Fact]
    public void A_width_the_old_presets_cannot_express_lands_on_the_widest_one()
    {
        var path = TempFile();

        new JsonSettingsStore(path).Save(AppSettings.Default with
        {
            ToolStyles = new Dictionary<ToolKind, ToolStyle>
            {
                [ToolKind.Line] = ToolStyle.Default.WithWidth(40),
            },
        });

        var written = new ToolStyles(new JsonSettingsStore(path).Load().ToolStyles!);

        // Three steps top out at five pixels, so going back a version cannot
        // give 40 back. What it must not do is come back thin.
        Assert.Equal(StrokeSize.Large, written.For(ToolKind.Line).Size);
    }

    /// <summary>
    /// What the released build puts on the screen for a stored preset. It has
    /// no width to read and multiplies the highlighter's preset by four itself.
    /// </summary>
    private static int DrawnByReleasedBuild(ToolKind kind, StrokeSize size)
    {
        var preset = size switch
        {
            StrokeSize.Small => 2,
            StrokeSize.Large => 5,
            _ => 3,
        };

        return kind == ToolKind.Marker ? preset * 4 : preset;
    }
}
