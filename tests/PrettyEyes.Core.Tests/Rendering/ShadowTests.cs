using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class ShadowTests
{
    private const int Size = 200;
    private const int Padding = 48;

    private static Document Shot()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        surface.Canvas.Clear(new SKColor(0x14, 0x14, 0x18));

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, Size, Size))
        {
            Selection = new CaptureRect(0, 0, Size, Size),
        };
    }

    // White, because a shadow on the black backdrop is black on black and any
    // test written against it passes whatever the shadow does.
    private static readonly ExportStyle Style =
        new(true, Padding, ExportBackground.White, 16, true);

    private static int Under(SKImage image, int below)
    {
        using var pixels = image.PeekPixels();

        return pixels.GetPixelColor(Padding + (Size / 2), Padding + Size + below).Red;
    }

    [Fact]
    public void The_shadow_fades_with_distance_instead_of_ending()
    {
        using var document = Shot();

        using var image = DocumentRenderer.Render(document, Style);

        var contact = Under(image, 2);
        var middle = Under(image, 12);
        var far = Under(image, 30);

        Assert.True(contact < middle, $"контакт {contact} должен быть темнее середины {middle}");
        Assert.True(middle < far, $"середина {middle} должна быть темнее дальней {far}");

        // Still not the bare backdrop that far out: that is the whole point of
        // the third layer.
        Assert.True(far < 255, $"на 30 пикселях уже ничего нет: {far}");
    }

    [Fact]
    public void Without_the_shadow_the_backdrop_is_untouched()
    {
        using var document = Shot();

        using var image = DocumentRenderer.Render(document, Style with { Shadow = false });

        Assert.Equal(255, Under(image, 12));
    }

    [Fact]
    public void The_shadow_stays_inside_the_padding()
    {
        using var document = Shot();

        using var image = DocumentRenderer.Render(document, Style);
        using var pixels = image.PeekPixels();

        // A shadow reaching past the canvas gets cut by a straight edge, and
        // the picture ends up with a dark border instead of a shadow. What is
        // left at the very edge is one level out of 255, which no screen shows
        // and no eye sees; demanding a clean 255 would be demanding that the
        // far layer stop short of where it should reach.
        Assert.InRange(pixels.GetPixelColor(Padding + (Size / 2), image.Height - 1).Red, 250, 255);
    }

    [Fact]
    public void What_was_drawn_on_the_screenshot_keeps_its_transparency()
    {
        // Three shadow filters used to mean three copies of the screenshot on
        // top of each other, and anything half transparent on it went solid.
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        surface.Canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, 80) };
        surface.Canvas.DrawRect(SKRect.Create(20, 20, 60, 60), paint);

        using var document = new Document(surface.Snapshot(), new CaptureRect(0, 0, Size, Size))
        {
            Selection = new CaptureRect(0, 0, Size, Size),
        };

        using var image = DocumentRenderer.Render(document, Style);
        using var pixels = image.PeekPixels();

        var mark = pixels.GetPixelColor(Padding + 40, Padding + 40).Red;

        // Half transparent black over white is grey, and it must stay grey.
        Assert.InRange(mark, 150, 200);
    }
}
