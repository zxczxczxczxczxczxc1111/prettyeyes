using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Annotations;

public class EmojiAnnotationTests
{
    private static SKImage NewGlyph()
    {
        using var surface = SKSurface.Create(new SKImageInfo(72, 72));
        surface.Canvas.Clear(SKColors.Yellow);

        return surface.Snapshot();
    }

    [Fact]
    public void The_glyph_lands_inside_its_own_bounds_and_nowhere_else()
    {
        using var glyph = NewGlyph();
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Black);

        new EmojiAnnotation(new CaptureRect(50, 50, 40, 40), glyph)
            .Draw(surface.Canvas, glyph, new CaptureRect(0, 0, 200, 200));

        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        Assert.Equal(SKColors.Yellow, pixels.GetPixelColor(70, 70));
        Assert.Equal(SKColors.Black, pixels.GetPixelColor(40, 40));
        Assert.Equal(SKColors.Black, pixels.GetPixelColor(120, 120));
    }

    [Fact]
    public void An_empty_region_draws_nothing_at_all()
    {
        using var glyph = NewGlyph();
        using var surface = SKSurface.Create(new SKImageInfo(50, 50));
        surface.Canvas.Clear(SKColors.Black);

        new EmojiAnnotation(CaptureRect.Empty, glyph)
            .Draw(surface.Canvas, glyph, new CaptureRect(0, 0, 50, 50));

        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        Assert.Equal(SKColors.Black, pixels.GetPixelColor(25, 25));
    }

    [Fact]
    public void A_click_on_a_small_selection_still_gets_a_readable_glyph()
    {
        // A sixth of 100 is under the minimum, and 16 pixels of emoji is a smudge.
        Assert.Equal(EmojiTool.MinSize, EmojiTool.DefaultSize(new CaptureRect(0, 0, 100, 100)));
    }

    [Fact]
    public void A_click_on_a_normal_selection_gets_a_sixth_of_the_shorter_side()
    {
        Assert.Equal(80, EmojiTool.DefaultSize(new CaptureRect(0, 0, 1200, 480)));
    }

    [Fact]
    public void A_click_on_a_huge_selection_is_capped()
    {
        Assert.Equal(EmojiTool.MaxSize, EmojiTool.DefaultSize(new CaptureRect(0, 0, 5120, 1440)));
    }

    [Fact]
    public void A_click_centres_the_glyph_on_the_pointer()
    {
        using var glyph = NewGlyph();
        var tool = new EmojiTool(glyph, new CaptureRect(0, 0, 1200, 600));

        tool.Begin(500, 300);
        var annotation = tool.End(502, 301);

        Assert.NotNull(annotation);

        var size = EmojiTool.DefaultSize(new CaptureRect(0, 0, 1200, 600));

        Assert.Equal(size, annotation!.Bounds.Width);
        Assert.Equal(500 - (size / 2), annotation.Bounds.X);
        Assert.Equal(300 - (size / 2), annotation.Bounds.Y);
    }

    [Fact]
    public void A_drag_sets_the_size_and_keeps_it_square()
    {
        using var glyph = NewGlyph();
        var tool = new EmojiTool(glyph, new CaptureRect(0, 0, 1200, 600));

        tool.Begin(100, 100);
        var annotation = tool.End(180, 140);

        Assert.NotNull(annotation);
        Assert.Equal(80, annotation!.Bounds.Width);
        Assert.Equal(80, annotation.Bounds.Height);
        Assert.Equal(100, annotation.Bounds.X);
        Assert.Equal(100, annotation.Bounds.Y);
    }

    [Fact]
    public void A_drag_the_other_way_starts_where_the_pointer_ended()
    {
        using var glyph = NewGlyph();
        var tool = new EmojiTool(glyph, new CaptureRect(0, 0, 1200, 600));

        tool.Begin(400, 400);
        var annotation = tool.End(340, 330);

        Assert.NotNull(annotation);
        Assert.Equal(70, annotation!.Bounds.Width);
        Assert.Equal(340, annotation.Bounds.X);
        Assert.Equal(330, annotation.Bounds.Y);
    }
}
