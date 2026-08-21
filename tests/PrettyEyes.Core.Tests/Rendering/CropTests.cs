using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

/// <summary>
/// Cutting one rectangle out of a document, annotations baked in. The pinned
/// window asks for this on its own schedule, so it cannot go on being a private
/// step of the export path.
/// </summary>
public class CropTests
{
    private static SKImage WhiteSource(int width = 100, int height = 100)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.White);

        return surface.Snapshot();
    }

    private static SKColor PixelAt(SKImage image, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(image);

        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public void Crop_is_the_size_of_the_area_asked_for()
    {
        using var document = new Document(WhiteSource(), new CaptureRect(0, 0, 100, 100));

        using var result = DocumentRenderer.Crop(document, new CaptureRect(10, 20, 40, 30));

        Assert.Equal(40, result.Width);
        Assert.Equal(30, result.Height);
    }

    [Fact]
    public void Crop_bakes_in_what_was_drawn_over_that_area()
    {
        using var document = new Document(WhiteSource(), new CaptureRect(0, 0, 100, 100));

        // A fat red outline over the middle of a white capture.
        document.Add(new RectangleAnnotation(new CaptureRect(30, 30, 40, 40), Palette.Red, 6f));

        using var result = DocumentRenderer.Crop(document, new CaptureRect(20, 20, 60, 60));

        // Ten pixels in from the crop's own corner is where that outline runs.
        Assert.NotEqual(SKColors.White, PixelAt(result, 10, 10));

        // And the corner of the crop itself is still the untouched capture.
        Assert.Equal(SKColors.White, PixelAt(result, 1, 1));
    }

    [Fact]
    public void An_area_outside_the_frame_is_refused_by_name()
    {
        using var document = new Document(WhiteSource(), new CaptureRect(0, 0, 100, 100));

        // Not "could not allocate a 0x0 surface", which is what a null surface
        // turns into three frames later and explains nothing.
        var thrown = Assert.Throws<InvalidOperationException>(
            () => DocumentRenderer.Crop(document, new CaptureRect(200, 200, 10, 10)));

        Assert.Contains("outside", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }
}
