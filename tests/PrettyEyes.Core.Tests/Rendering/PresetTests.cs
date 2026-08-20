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
    public void The_sheet_keeps_the_shadow_and_drops_the_grain()
    {
        var sheet = ExportStyle.Sheet;

        Assert.Equal(ExportBackground.White, sheet.Background);
        Assert.True(sheet.Shadow);

        // White is chosen for being clean, and grain on it is dirt.
        Assert.False(sheet.Grain);
        Assert.False(sheet.GrainAllowed);
    }

    [Fact]
    public void No_preset_promises_a_transparency_the_clipboard_cannot_carry()
    {
        // The clipboard format Windows pastes from has no alpha channel, so a
        // preset offering a cut-out hands out a white sheet wherever it lands.
        // Transparency stays available under the parameters, for files.
        Assert.NotEqual(ExportBackground.Transparent, ExportStyle.Card.Background);
        Assert.NotEqual(ExportBackground.Transparent, ExportStyle.Sheet.Background);
    }

    [Fact]
    public void A_preset_is_recognised_by_being_equal_to_itself()
    {
        // This is what the settings window highlights on, so it has to hold:
        // no stored "which preset is chosen", nothing to fall out of step.
        Assert.Equal(ExportStyle.Card, ExportStyle.Card with { });
        Assert.NotEqual(ExportStyle.Card, ExportStyle.Card with { Padding = 24 });
        Assert.NotEqual(ExportStyle.Card, ExportStyle.Sheet);
    }

    [Fact]
    public void Turning_the_decoration_off_is_the_style_that_does_nothing()
    {
        Assert.False(ExportStyle.None.Enabled);
    }
}
