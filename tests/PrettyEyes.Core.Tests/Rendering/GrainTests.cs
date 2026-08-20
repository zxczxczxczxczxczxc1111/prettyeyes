using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class GrainTests
{
    private static Document Shot(int size = 200)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        surface.Canvas.Clear(new SKColor(0x14, 0x14, 0x18));

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, size, size))
        {
            Selection = new CaptureRect(0, 0, size, size),
        };
    }

    private static byte[] Pixels(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);

        return bitmap.Bytes;
    }

    [Fact]
    public void Two_exports_of_one_screenshot_are_the_same_down_to_the_pixel()
    {
        using var document = Shot();
        var style = new ExportStyle(true, 48, ExportBackground.Aura, 16, false);

        using var first = DocumentRenderer.Render(document, style);
        using var second = DocumentRenderer.Render(document, style);

        // Pixels, not the encoded file: the PNG codec brings a determinism
        // question of its own to a test that is not about it.
        Assert.Equal(Pixels(first), Pixels(second));
    }

    [Fact]
    public void The_tile_is_anchored_to_the_canvas_not_to_the_picture()
    {
        // Aura over a flat shot: the haze is the same colour whatever the
        // canvas size, so anything that differs at one point is the grain.
        var style = new ExportStyle(true, 48, ExportBackground.Aura, 16, false);

        using var small = Shot(120);
        using var large = Shot(400);

        using var one = DocumentRenderer.Render(small, style);
        using var other = DocumentRenderer.Render(large, style);

        using var onePixels = one.PeekPixels();
        using var otherPixels = other.PeekPixels();

        Assert.Equal(onePixels.GetPixelColor(7, 11), otherPixels.GetPixelColor(7, 11));
    }

    [Fact]
    public void Grain_leaves_the_backdrop_as_light_as_it_found_it()
    {
        using var document = Shot();

        var grainy = new ExportStyle(true, 48, ExportBackground.Aura, 16, false);
        var plain = grainy with { Grain = false };

        using var withGrain = DocumentRenderer.Render(document, grainy);
        using var without = DocumentRenderer.Render(document, plain);

        // Not to the decimal: half the specks lighten and half darken, but the
        // two halves never land on exactly the same pixels.
        Assert.True(Math.Abs(Average(without) - Average(withGrain)) < 2);
    }

    [Fact]
    public void A_flat_backdrop_keeps_its_exact_colour()
    {
        using var document = Shot();
        var style = new ExportStyle(true, 48, ExportBackground.White, 16, false);

        using var image = DocumentRenderer.Render(document, style);
        using var pixels = image.PeekPixels();

        // White is chosen when the screenshot has to land on a clean page.
        // Grain there is dirt, not texture.
        Assert.Equal(SKColors.White, pixels.GetPixelColor(3, 3));
    }

    [Fact]
    public void The_transparent_backdrop_stays_empty()
    {
        using var document = Shot();
        var style = new ExportStyle(true, 48, ExportBackground.Transparent, 16, false);

        using var image = DocumentRenderer.Render(document, style);
        using var pixels = image.PeekPixels();

        // Grain over nothing is an even haze of noise where the transparency
        // was supposed to be.
        Assert.Equal(0, pixels.GetPixelColor(3, 3).Alpha);
        Assert.Equal(0, pixels.GetPixelColor(9, 5).Alpha);
    }

    private static double Average(SKImage image)
    {
        using var pixels = image.PeekPixels();

        var total = 0.0;
        var counted = 0;

        // The backdrop only: the shot in the middle is the same either way.
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var colour = pixels.GetPixelColor(x, y);
                total += (colour.Red + colour.Green + colour.Blue) / 3.0;
                counted++;
            }
        }

        return total / counted;
    }
}
