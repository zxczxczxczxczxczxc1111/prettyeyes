using PrettyEyes.Core.Annotations;
using Xunit;

namespace PrettyEyes.Core.Tests.Annotations;

public class FreehandArrowAnnotationTests
{
    [Fact]
    public void The_head_points_the_way_the_line_arrived()
    {
        // Straight to the right: the head has to face east, which is angle zero.
        int[] x = [0, 40, 80, 120];
        int[] y = [10, 10, 10, 10];

        var angle = FreehandArrowAnnotation.HeadAngle(x, y, strokeWidth: 3f);

        Assert.NotNull(angle);
        Assert.Equal(0, angle!.Value, precision: 3);
    }

    [Fact]
    public void A_shaking_hand_at_the_end_does_not_spin_the_head()
    {
        // The last two points are a millimetre of tremor pointing north, and
        // the segment between them is the whole direction a naive reading
        // would use. The line arrived from the west and the head has to say so.
        int[] x = [0, 40, 80, 120, 120];
        int[] y = [10, 10, 10, 10, 8];

        var angle = FreehandArrowAnnotation.HeadAngle(x, y, strokeWidth: 3f);

        Assert.NotNull(angle);
        Assert.True(Math.Abs(angle!.Value) < 0.35, $"голова смотрит в {angle.Value} рад");
    }

    [Fact]
    public void A_stroke_shorter_than_the_head_gets_no_head()
    {
        // Three pixels of movement is a dot, and a dot with an arrowhead on it
        // is a blot: nothing in it says which way it points.
        int[] x = [0, 2, 3];
        int[] y = [0, 0, 1];

        Assert.Null(FreehandArrowAnnotation.HeadAngle(x, y, strokeWidth: 3f));
    }

    [Fact]
    public void Too_few_points_to_have_a_direction()
    {
        // Reachable: the method is public so the angle can be checked without a
        // canvas, and a caller with an empty gesture would otherwise index off
        // the end of the array.
        Assert.Null(FreehandArrowAnnotation.HeadAngle([], [], strokeWidth: 3f));
        Assert.Null(FreehandArrowAnnotation.HeadAngle([5], [5], strokeWidth: 3f));

        // Mismatched lengths mean somebody built the arrays wrong; answering
        // "no direction" beats indexing into the shorter one.
        Assert.Null(FreehandArrowAnnotation.HeadAngle([0, 50], [0], strokeWidth: 3f));
    }

    [Fact]
    public void Bounds_leave_room_for_the_head()
    {
        var arrow = new FreehandArrowAnnotation(
            [(20, 20), (60, 20), (100, 20)], color: 0xFFB01030, strokeWidth: 3f);

        // The branches reach back and sideways from the tip, and Bounds is what
        // Document.MovableAt hit-tests against: a box that ends at the last
        // point makes the head unclickable.
        var reach = (int)Math.Ceiling(ArrowHead.Length(3f));

        Assert.True(arrow.Bounds.X <= 20 - reach);
        Assert.True(arrow.Bounds.Y <= 20 - reach);
        Assert.True(arrow.Bounds.Right >= 100 + reach);
        Assert.True(arrow.Bounds.Bottom >= 20 + reach);
    }

    [Fact]
    public void The_points_are_copied_on_the_way_in()
    {
        // The tool keeps drawing into its own list while this one is already on
        // the canvas: sharing it would let a finished arrow grow a tail.
        var points = new List<(int X, int Y)> { (0, 0), (50, 0) };
        var arrow = new FreehandArrowAnnotation(points, color: 0xFFB01030, strokeWidth: 3f);
        var before = arrow.Bounds;

        points.Add((900, 900));

        Assert.Equal(before, arrow.Bounds);
    }
}
