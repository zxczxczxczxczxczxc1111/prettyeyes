using PrettyEyes.Core.Annotations;
using Xunit;

namespace PrettyEyes.Core.Tests.Annotations;

public class StrokeAnnotationTests
{
    [Fact]
    public void The_bounds_cover_the_line_and_half_its_width()
    {
        // Padding is half the stroke plus one: a three pixel line drawn along
        // y = 20 paints from 18 to 22, and Bounds has to claim all of it.
        var stroke = new StrokeAnnotation(
            [(10, 20), (30, 20), (50, 60)], 0xFFE5484D, strokeWidth: 3, highlighter: false);

        Assert.Equal(7, stroke.Bounds.X);
        Assert.Equal(17, stroke.Bounds.Y);
        Assert.Equal(46, stroke.Bounds.Width);
        Assert.Equal(46, stroke.Bounds.Height);
    }

    [Fact]
    public void A_single_point_still_has_a_box_around_it()
    {
        var dot = new StrokeAnnotation([(100, 100)], 0xFFFFFFFF, strokeWidth: 6, highlighter: false);

        Assert.Equal(96, dot.Bounds.X);
        Assert.Equal(96, dot.Bounds.Y);
        Assert.Equal(8, dot.Bounds.Width);
        Assert.Equal(8, dot.Bounds.Height);
    }

    [Fact]
    public void Negative_coordinates_are_kept()
    {
        // A monitor left of the primary one starts at a negative origin, and
        // every sampling bug in this project has been a lost minus sign.
        var stroke = new StrokeAnnotation(
            [(-200, -80), (-100, -40)], 0xFFE5484D, strokeWidth: 1, highlighter: false);

        Assert.Equal(-202, stroke.Bounds.X);
        Assert.Equal(-82, stroke.Bounds.Y);
        Assert.Equal(104, stroke.Bounds.Width);
        Assert.Equal(44, stroke.Bounds.Height);
    }

    [Fact]
    public void The_order_of_the_points_does_not_move_the_box()
    {
        var forward = new StrokeAnnotation(
            [(10, 10), (90, 70)], 0xFFE5484D, strokeWidth: 3, highlighter: false);

        var backward = new StrokeAnnotation(
            [(90, 70), (10, 10)], 0xFFE5484D, strokeWidth: 3, highlighter: false);

        Assert.Equal(forward.Bounds, backward.Bounds);
    }
}
