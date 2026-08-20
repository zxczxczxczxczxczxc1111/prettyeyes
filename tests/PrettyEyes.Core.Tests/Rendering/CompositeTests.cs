using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class CompositeTests
{
    private static SKImage Sample()
    {
        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);

        // Half transparent black, the way a soft shadow arrives.
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, 128) };
        surface.Canvas.DrawRect(SKRect.Create(10, 0, 10, 20), paint);

        return surface.Snapshot();
    }

    [Fact]
    public void What_was_empty_becomes_the_colour_it_was_laid_on()
    {
        using var image = Sample();

        using var flat = DocumentRenderer.Composite(image, SKColors.White);
        using var pixels = flat.PeekPixels();

        Assert.Equal(SKColors.White, pixels.GetPixelColor(2, 2));
    }

    [Fact]
    public void A_soft_shadow_becomes_grey_rather_than_black()
    {
        using var image = Sample();

        using var flat = DocumentRenderer.Composite(image, SKColors.White);
        using var pixels = flat.PeekPixels();

        var shadow = pixels.GetPixelColor(15, 10);

        // This is the whole point. Forcing the alpha byte to 255 without laying
        // the picture on anything - which is what the clipboard used to do -
        // turns this pixel solid black.
        Assert.Equal(255, shadow.Alpha);
        Assert.InRange(shadow.Red, 100, 160);
    }

    [Fact]
    public void The_size_is_the_size_it_was()
    {
        using var image = Sample();

        using var flat = DocumentRenderer.Composite(image, SKColors.White);

        Assert.Equal(image.Width, flat.Width);
        Assert.Equal(image.Height, flat.Height);
    }
}
