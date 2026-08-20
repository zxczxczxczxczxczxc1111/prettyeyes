using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class SheenTests
{
    private const int Size = 200;
    private const int Padding = 48;

    private static Document Shot()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        surface.Canvas.Clear(new SKColor(0x30, 0x30, 0x36));

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, Size, Size))
        {
            Selection = new CaptureRect(0, 0, Size, Size),
        };
    }

    private static readonly ExportStyle Style =
        new(true, Padding, ExportBackground.Black, 16, false);

    private static int Inside(SKImage image, int fromTop)
    {
        using var pixels = image.PeekPixels();

        return pixels.GetPixelColor(Padding + (Size / 2), Padding + fromTop).Red;
    }

    [Fact]
    public void The_top_of_the_card_catches_the_light()
    {
        using var document = Shot();

        using var lit = DocumentRenderer.Render(document, Style);
        using var flat = DocumentRenderer.Render(document, Style with { Sheen = false });

        Assert.True(Inside(lit, 4) > Inside(flat, 4));
    }

    [Fact]
    public void The_bottom_of_the_card_is_left_alone()
    {
        using var document = Shot();

        using var lit = DocumentRenderer.Render(document, Style);
        using var flat = DocumentRenderer.Render(document, Style with { Sheen = false });

        // Six pixels in, clear of the rim: the light itself is gone by half way
        // down, and the screenshot below must be the screenshot.
        Assert.Equal(Inside(flat, Size - 6), Inside(lit, Size - 6));
    }

    [Fact]
    public void The_card_gets_an_edge()
    {
        using var document = Shot();

        using var lit = DocumentRenderer.Render(document, Style);
        using var flat = DocumentRenderer.Render(document, Style with { Sheen = false });

        using var litPixels = lit.PeekPixels();
        using var flatPixels = flat.PeekPixels();

        var x = Padding + (Size / 2);
        var bottom = Padding + Size - 1;

        Assert.True(litPixels.GetPixelColor(x, bottom).Red > flatPixels.GetPixelColor(x, bottom).Red);
    }
}
