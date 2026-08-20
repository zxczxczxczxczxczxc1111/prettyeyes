using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class ExportStyleTests
{
    private static Document NewDocument(int width = 200, int height = 200)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Blue);

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, width, height))
        {
            Selection = new CaptureRect(20, 20, 100, 100),
        };
    }

    [Fact]
    public void Switched_off_it_renders_exactly_what_it_always_did()
    {
        using var document = NewDocument();

        using var plain = DocumentRenderer.Render(document);
        using var styled = DocumentRenderer.Render(document, ExportStyle.None);

        Assert.Equal(plain.Width, styled.Width);
        Assert.Equal(plain.Height, styled.Height);
    }

    [Fact]
    public void Padding_grows_the_picture_on_every_side()
    {
        using var document = NewDocument();
        var style = new ExportStyle(true, 24, ExportBackground.Black, 0, false);

        using var image = DocumentRenderer.Render(document, style);

        Assert.Equal(100 + 48, image.Width);
        Assert.Equal(100 + 48, image.Height);
    }

    [Fact]
    public void A_transparent_backdrop_leaves_the_corner_see_through()
    {
        using var document = NewDocument();
        var style = new ExportStyle(true, 24, ExportBackground.Transparent, 0, false);

        using var image = DocumentRenderer.Render(document, style);
        using var pixels = image.PeekPixels();

        Assert.Equal(0, pixels.GetPixelColor(2, 2).Alpha);
    }

    [Fact]
    public void A_white_backdrop_is_white_in_the_corner_and_the_shot_is_still_there()
    {
        using var document = NewDocument();
        var style = new ExportStyle(true, 24, ExportBackground.White, 0, false);

        using var image = DocumentRenderer.Render(document, style);
        using var pixels = image.PeekPixels();

        Assert.Equal(SKColors.White, pixels.GetPixelColor(2, 2));
        Assert.Equal(SKColors.Blue, pixels.GetPixelColor(74, 74));
    }

    [Fact]
    public void Rounding_eats_the_corner_pixel_of_the_shot()
    {
        using var document = NewDocument();
        var style = new ExportStyle(true, 24, ExportBackground.White, 16, false);

        using var image = DocumentRenderer.Render(document, style);
        using var pixels = image.PeekPixels();

        // Just inside the shot's own corner: rounded away, so the backdrop shows.
        Assert.Equal(SKColors.White, pixels.GetPixelColor(25, 25));
        Assert.Equal(SKColors.Blue, pixels.GetPixelColor(74, 74));
    }

    [Fact]
    public void A_tiny_selection_does_not_get_a_frame_bigger_than_itself()
    {
        using var document = NewDocument();
        document.Selection = new CaptureRect(20, 20, 40, 40);

        var style = new ExportStyle(true, 72, ExportBackground.Black, 16, true);

        using var image = DocumentRenderer.Render(document, style);

        // Padding capped at a quarter of the shorter side: 10, not 72.
        Assert.Equal(40 + 20, image.Width);
    }

    [Fact]
    public void A_shadow_needs_padding_to_fall_on()
    {
        Assert.False(new ExportStyle(true, 0, ExportBackground.Black, 0, true).ShadowAllowed);
        Assert.True(new ExportStyle(true, 24, ExportBackground.Black, 0, true).ShadowAllowed);
    }

    [Fact]
    public void Fitting_a_style_to_a_size_leaves_a_disabled_one_alone()
    {
        var fitted = new ExportStyle(false, 48, ExportBackground.White, 8, true).FitTo(1000, 800);

        Assert.Equal(ExportStyle.None, fitted);
    }
}
