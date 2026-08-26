using PrettyEyes.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class MagnifierReadoutTests
{
    [Fact]
    public void A_pixel_reads_as_its_place_and_its_colour()
    {
        var readout = MagnifierReadout.ForPixel(120, 44, new SKColor(0x2E, 0x7D, 0x32));

        Assert.Equal("120, 44", readout.Position);
        Assert.Equal("#2E7D32", readout.Value);
        Assert.Equal(new SKColor(0x2E, 0x7D, 0x32), readout.Swatch);
    }

    [Fact]
    public void Off_the_frame_there_is_no_colour_to_name()
    {
        var readout = MagnifierReadout.ForPixel(-5, -5, colour: null);

        Assert.Equal("-5, -5", readout.Position);
        Assert.Equal("-", readout.Value);
        Assert.Null(readout.Swatch);
    }

    [Fact]
    public void While_a_selection_is_dragged_its_size_matters_more()
    {
        var readout = MagnifierReadout.ForSize(new CaptureRect(10, 10, 640, 480));

        Assert.Equal("640 x 480", readout.Position);
        Assert.Equal(string.Empty, readout.Value);
        Assert.Null(readout.Swatch);
    }

    [Fact]
    public void The_copied_line_says_what_went_to_the_clipboard()
    {
        var readout = MagnifierReadout.Copied(new SKColor(0xFF, 0x00, 0x7F));

        Assert.Equal("#FF007F", readout.Position);
        Assert.Equal("скопирован", readout.Value);
        Assert.Equal(new SKColor(0xFF, 0x00, 0x7F), readout.Swatch);
    }

    [Fact]
    public void The_same_pixel_reads_the_same_twice()
    {
        // The whole point of the type: the label repaints only when this
        // comparison says something changed, and the mouse reports the same
        // pixel dozens of times in a row.
        var first = MagnifierReadout.ForPixel(7, 8, SKColors.White);
        var second = MagnifierReadout.ForPixel(7, 8, SKColors.White);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_different_pixel_reads_differently()
    {
        var first = MagnifierReadout.ForPixel(7, 8, SKColors.White);
        var second = MagnifierReadout.ForPixel(7, 9, SKColors.White);

        Assert.NotEqual(first, second);
    }
}
