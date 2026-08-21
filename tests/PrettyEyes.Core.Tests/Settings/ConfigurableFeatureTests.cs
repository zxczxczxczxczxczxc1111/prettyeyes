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
    public void The_default_tool_choice_holds_drawing_tools_and_no_emoji()
    {
        var choices = ConfigurableFeature.DefaultToolChoices;

        Assert.Contains(ToolKind.Arrow, choices);
        Assert.DoesNotContain(ToolKind.Emoji, choices);
    }
}
