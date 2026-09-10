using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Settings;

public class ConfigurableFeatureTests
{
    [Fact]
    public void The_magnifier_is_not_a_drawing_tool()
    {
        var magnifier = ConfigurableFeature.All.Single(f => f.Id == FeatureId.Magnifier);

        Assert.Equal(FeatureGroup.Feature, magnifier.Group);
    }

    [Fact]
    public void Blur_has_nothing_to_configure()
    {
        // Blur has no colour and no width. The row has to say so with a mark,
        // not leave people right-clicking at nothing.
        var blur = ConfigurableFeature.All.Single(f => f.Id == FeatureId.Blur);

        Assert.False(blur.HasSettings);
    }

    [Fact]
    public void Emoji_opens_the_grid_of_glyphs_and_not_the_colour_card()
    {
        // Colour and width mean nothing to a stamped glyph: EmojiTool never
        // reads a style. The settings window used to open the colour card here
        // anyway, which is somebody else's card wearing the emoji title.
        var emoji = ConfigurableFeature.All.Single(f => f.Id == FeatureId.Emoji);

        Assert.Equal(FeatureCard.Emoji, emoji.Card);
    }

    [Fact]
    public void The_drawing_tools_that_are_not_emoji_open_the_colour_card()
    {
        var pencil = ConfigurableFeature.All.Single(f => f.Id == FeatureId.Pencil);

        Assert.Equal(FeatureCard.Style, pencil.Card);
    }

    /// <summary>
    /// The pointer draws on the screen and leaves nothing behind. Made a
    /// ToolKind it would be an annotation: it would go into the document, into
    /// the undo stack and into the exported picture, and it would be offerable
    /// as the tool a capture starts with. This is the gate against that, and it
    /// is a gate rather than a comment because the row it sits in is full of
    /// things that ARE tools.
    /// </summary>
    [Fact]
    public void The_pointer_is_not_a_drawing_tool_and_never_becomes_an_annotation()
    {
        var laser = ConfigurableFeature.All.Single(f => f.Id == FeatureId.Laser);

        Assert.Equal(FeatureGroup.Feature, laser.Group);
        Assert.Null(laser.Tool);
        Assert.DoesNotContain(
            Enum.GetNames<ToolKind>(),
            name => name.Contains("laser", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_default_tool_choice_holds_drawing_tools_and_no_emoji()
    {
        var choices = ConfigurableFeature.DefaultToolChoices;

        Assert.Contains(ToolKind.Arrow, choices);
        Assert.DoesNotContain(ToolKind.Emoji, choices);
    }
}
