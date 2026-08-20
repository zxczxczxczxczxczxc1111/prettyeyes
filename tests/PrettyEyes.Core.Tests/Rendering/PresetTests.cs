using PrettyEyes.Core.Rendering;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class PresetTests
{
    [Fact]
    public void The_card_gets_everything_that_makes_it_an_object()
    {
        var card = ExportStyle.Card;

        Assert.True(card.Enabled);
        Assert.Equal(ExportBackground.Aura, card.Background);
        Assert.True(card.Shadow);
        Assert.True(card.ShadowAllowed);
        Assert.True(card.GrainAllowed);
        Assert.True(card.Sheen);
    }

    [Fact]
    public void The_cutout_keeps_the_shadow_and_drops_the_grain()
    {
        var cutout = ExportStyle.Cutout;

        Assert.Equal(ExportBackground.Transparent, cutout.Background);
        Assert.True(cutout.Shadow);

        // Grain over nothing is noise where the transparency was meant to be.
        Assert.False(cutout.Grain);
        Assert.False(cutout.GrainAllowed);
    }

    [Fact]
    public void A_preset_is_recognised_by_being_equal_to_itself()
    {
        // This is what the settings window highlights on, so it has to hold:
        // no stored "which preset is chosen", nothing to fall out of step.
        Assert.Equal(ExportStyle.Card, ExportStyle.Card with { });
        Assert.NotEqual(ExportStyle.Card, ExportStyle.Card with { Padding = 24 });
        Assert.NotEqual(ExportStyle.Card, ExportStyle.Cutout);
    }

    [Fact]
    public void Turning_the_decoration_off_is_the_style_that_does_nothing()
    {
        Assert.False(ExportStyle.None.Enabled);
    }
}
