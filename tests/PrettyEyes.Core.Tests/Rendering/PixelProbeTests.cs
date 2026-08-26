using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class PixelProbeTests
{
    /// <summary>
    /// A picture with a known left half and a known right half, placed
    /// somewhere with negative coordinates: a monitor left of the primary one
    /// starts there, and every sampling bug in this project has been a missing
    /// subtraction of the frame origin.
    /// </summary>
    private static SKImage Halves(SKColor left, SKColor right, int width = 40, int height = 20)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(left);

        using var paint = new SKPaint { Color = right };
        canvas.DrawRect(SKRect.Create(width / 2f, 0, width / 2f, height), paint);

        return SKImage.FromBitmap(bitmap);
    }

    [Fact]
    public void The_colour_is_read_in_virtual_desktop_coordinates()
    {
        using var image = Halves(SKColors.Black, SKColors.White);
        using var probe = new PixelProbe(image, new CaptureRect(-100, -50, 40, 20));

        Assert.Equal(SKColors.Black, probe.ColourAt(-95, -45));
        Assert.Equal(SKColors.White, probe.ColourAt(-70, -45));
    }

    [Fact]
    public void A_point_off_the_frame_has_no_colour()
    {
        using var image = Halves(SKColors.Black, SKColors.White);
        using var probe = new PixelProbe(image, new CaptureRect(-100, -50, 40, 20));

        Assert.Null(probe.ColourAt(-101, -45));
        Assert.Null(probe.ColourAt(-95, -51));
        Assert.Null(probe.ColourAt(-60, -45));
        Assert.Null(probe.ColourAt(-95, -30));
    }

    [Fact]
    public void White_reads_as_bright_and_black_as_dark()
    {
        using var white = Halves(SKColors.White, SKColors.White);
        using var probe = new PixelProbe(white, new CaptureRect(0, 0, 40, 20));

        Assert.Equal(1.0, probe.LuminanceAround(20, 10, reach: 8)!.Value, precision: 2);

        using var black = Halves(SKColors.Black, SKColors.Black);
        using var dark = new PixelProbe(black, new CaptureRect(0, 0, 40, 20));

        Assert.Equal(0.0, dark.LuminanceAround(20, 10, reach: 8)!.Value, precision: 2);
    }

    [Fact]
    public void An_edge_is_answered_by_what_is_around_it_not_by_one_pixel()
    {
        // The whole reason the reading is an average: the cursor covers a
        // couple of dozen pixels, and a decision taken from the single pixel
        // under its tip flips on every speck of dust.
        using var image = Halves(SKColors.Black, SKColors.White);
        using var probe = new PixelProbe(image, new CaptureRect(0, 0, 40, 20));

        var atEdge = probe.LuminanceAround(20, 10, reach: 8)!.Value;

        Assert.InRange(atEdge, 0.2, 0.8);
    }

    [Fact]
    public void Samples_that_fall_off_the_frame_are_skipped_not_counted_as_black()
    {
        // A cursor near the left edge used to be told the screen was darker
        // than it is, because everything past the edge weighed in as zero.
        using var image = Halves(SKColors.White, SKColors.White);
        using var probe = new PixelProbe(image, new CaptureRect(0, 0, 40, 20));

        Assert.Equal(1.0, probe.LuminanceAround(0, 0, reach: 12)!.Value, precision: 2);
    }

    [Fact]
    public void A_point_far_off_the_frame_has_no_reading_at_all()
    {
        using var image = Halves(SKColors.White, SKColors.White);
        using var probe = new PixelProbe(image, new CaptureRect(0, 0, 40, 20));

        Assert.Null(probe.LuminanceAround(500, 500, reach: 12));
    }
}
