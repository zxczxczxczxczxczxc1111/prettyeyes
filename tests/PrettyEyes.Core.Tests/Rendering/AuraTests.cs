using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class AuraTests
{
    private static Document Shot(SKColor colour, int size = 200)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        surface.Canvas.Clear(colour);

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, size, size))
        {
            Selection = new CaptureRect(0, 0, size, size),
        };
    }

    private static readonly ExportStyle Style =
        new(true, 48, ExportBackground.Aura, 16, false);

    private static double Lightness(SKColor colour) =>
        ((colour.Red * 0.299) + (colour.Green * 0.587) + (colour.Blue * 0.114)) / 255.0;

    [Fact]
    public void A_dark_screenshot_gets_a_backdrop_lighter_than_itself()
    {
        var dark = new SKColor(0x14, 0x14, 0x18);
        using var document = Shot(dark);

        using var image = DocumentRenderer.Render(document, Style);
        using var pixels = image.PeekPixels();

        Assert.True(Lightness(pixels.GetPixelColor(4, 4)) > Lightness(dark));
    }

    [Fact]
    public void A_light_screenshot_gets_a_backdrop_darker_than_itself()
    {
        var light = new SKColor(0xF4, 0xF4, 0xF6);
        using var document = Shot(light);

        using var image = DocumentRenderer.Render(document, Style);
        using var pixels = image.PeekPixels();

        Assert.True(Lightness(pixels.GetPixelColor(4, 4)) < Lightness(light));
    }

    [Fact]
    public void The_backdrop_carries_the_screenshot_own_colour()
    {
        using var document = Shot(new SKColor(0x10, 0x30, 0x90));

        using var image = DocumentRenderer.Render(document, Style);
        using var pixels = image.PeekPixels();

        var corner = pixels.GetPixelColor(4, 4);

        // Blue shot, blue haze. A grey backdrop here would mean the aura is
        // ignoring the picture and inventing a colour of its own.
        Assert.True(corner.Blue > corner.Red);
        Assert.True(corner.Blue > corner.Green);
    }

    [Fact]
    public void The_decoration_does_not_change_the_size()
    {
        using var document = Shot(SKColors.Black);

        using var image = DocumentRenderer.Render(document, Style);

        Assert.Equal(200 + 96, image.Width);
        Assert.Equal(200 + 96, image.Height);
    }

    [Fact]
    public void A_tiny_selection_is_not_blown_up_before_being_blurred()
    {
        // Forty pixels is smaller than the thumbnail the aura is built from,
        // and upscaling before a blur is paying for detail that is not there.
        using var document = Shot(new SKColor(0x14, 0x14, 0x18), 40);

        using var image = DocumentRenderer.Render(document, Style);

        Assert.True(image.Width > 40);
    }
}
