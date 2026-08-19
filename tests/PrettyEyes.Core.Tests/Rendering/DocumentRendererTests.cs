using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class DocumentRendererTests
{
    /// <summary>
    /// Left half white, right half black - lets us tell crops apart by colour.
    /// </summary>
    private static SKImage TwoToneSource(int width = 100, int height = 100)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(width / 2f, 0, width / 2f, height, paint);

        return surface.Snapshot();
    }

    private static SKColor PixelAt(SKImage image, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public void Render_crops_to_selection_size()
    {
        using var document = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(10, 20, 40, 30),
        };

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(40, result.Width);
        Assert.Equal(30, result.Height);
    }

    [Fact]
    public void Render_takes_pixels_from_selected_region()
    {
        using var document = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            // Entirely inside the black right half.
            Selection = new CaptureRect(60, 10, 20, 20),
        };

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(SKColors.Black, PixelAt(result, 5, 5));
    }

    [Fact]
    public void Render_respects_a_negative_source_origin()
    {
        // Frame starts at -100: virtual x=-40 is pixel 60 of the image, black.
        using var document = new Document(TwoToneSource(), new CaptureRect(-100, 0, 100, 100))
        {
            Selection = new CaptureRect(-40, 10, 20, 20),
        };

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(SKColors.Black, PixelAt(result, 5, 5));
    }

    [Fact]
    public void Render_clamps_a_selection_reaching_outside_the_frame()
    {
        using var document = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(90, 90, 50, 50),
        };

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void Render_without_selection_uses_whole_source()
    {
        using var document = new Document(TwoToneSource(80, 60), new CaptureRect(0, 0, 80, 60))
        {
            Selection = CaptureRect.Empty,
        };

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(80, result.Width);
        Assert.Equal(60, result.Height);
    }

    [Fact]
    public void Render_draws_rectangle_annotation()
    {
        using var document = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(0, 0, 50, 50),
        };
        document.Add(new RectangleAnnotation(new CaptureRect(0, 0, 50, 50), 0xFFFF0000, 4f));

        using var result = DocumentRenderer.Render(document);

        Assert.Equal(SKColors.Red, PixelAt(result, 25, 1));
    }

    [Fact]
    public void Render_blur_changes_pixels_on_a_sharp_edge()
    {
        using var document = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(0, 0, 100, 100),
        };
        // Across the white/black border - those pixels must turn grey.
        document.Add(new BlurAnnotation(new CaptureRect(30, 30, 40, 40)));

        using var result = DocumentRenderer.Render(document);
        var pixel = PixelAt(result, 50, 50);

        Assert.NotEqual(SKColors.White, pixel);
        Assert.NotEqual(SKColors.Black, pixel);
    }

    [Fact]
    public void Render_blur_does_not_stack_when_regions_overlap()
    {
        using var single = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(0, 0, 100, 100),
        };
        single.Add(new BlurAnnotation(new CaptureRect(30, 30, 40, 40)));

        using var doubled = new Document(TwoToneSource(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(0, 0, 100, 100),
        };
        doubled.Add(new BlurAnnotation(new CaptureRect(30, 30, 40, 40)));
        doubled.Add(new BlurAnnotation(new CaptureRect(30, 30, 40, 40)));

        using var a = DocumentRenderer.Render(single);
        using var b = DocumentRenderer.Render(doubled);

        // Both sample the pristine source, so the result is identical.
        Assert.Equal(PixelAt(a, 50, 50), PixelAt(b, 50, 50));
    }
}
