using PrettyEyes.Core.Annotations;
using Xunit;

namespace PrettyEyes.Core.Tests.Annotations;

public class FreehandArrowAnnotationTests
{
    [Fact]
    public void The_head_points_the_way_the_line_arrived()
    {
        int[] x = [0, 40, 80, 120];
        int[] y = [10, 10, 10, 10];

        var angle = FreehandArrowAnnotation.HeadAngle(x, y);

        Assert.NotNull(angle);
        Assert.Equal(0, angle!.Value, precision: 3);
    }

    [Fact]
    public void A_shaking_hand_at_the_end_does_not_spin_the_head()
    {
        // The complaint itself: drawing slowly towards something small. Points
        // land two pixels apart, which is as close together as the tool keeps
        // them, and the hand wanders a pixel either way the whole time. On the
        // old rule a thin line read its direction off four pixels of this and
        // came out up to 45 degrees off.
        var x = new List<int>();
        var y = new List<int>();

        for (var i = 0; i <= 60; i++)
        {
            x.Add(i * 2);
            y.Add(i % 2 == 0 ? 1 : -1);
        }

        var angle = FreehandArrowAnnotation.HeadAngle(x, y);

        Assert.NotNull(angle);
        Assert.True(Math.Abs(angle!.Value) < 0.12, $"голова смотрит в {angle.Value} рад");
    }

    [Fact]
    public void One_big_slip_at_the_very_end_still_tilts_the_head_a_little()
    {
        // Not a regression, a limit worth writing down. The line runs east and
        // the hand slips eight pixels sideways on the last sample: the last
        // segment alone points 76 degrees off, and that segment used to be the
        // whole answer. Over a tail it comes to 20, because the tip is that
        // stray point and the head is anchored to it. Only a flick does this;
        // the slow drawing this change is about is the test above.
        int[] x = [0, 20, 40, 60, 62];
        int[] y = [0, 0, 0, 0, 8];

        var angle = FreehandArrowAnnotation.HeadAngle(x, y);

        Assert.NotNull(angle);
        Assert.True(Math.Abs(angle!.Value) < 0.40, $"голова смотрит в {angle.Value} рад");
    }

    [Fact]
    public void A_short_but_deliberate_arrow_still_gets_a_head()
    {
        // Twelve pixels is shorter than the tail and much shorter than a thick
        // arrow's head. It is still an arrow somebody drew on purpose, and the
        // old rule - no head unless the stroke beats four times the width -
        // left a fat short arrow with no head at all.
        int[] x = [0, 4, 8, 12];
        int[] y = [0, 0, 0, 0];

        Assert.NotNull(FreehandArrowAnnotation.HeadAngle(x, y));
    }

    [Fact]
    public void A_stroke_too_short_to_have_a_direction_gets_no_head()
    {
        // Three pixels is a dot, and a dot with an arrowhead on it is a blot.
        int[] x = [0, 2, 3];
        int[] y = [0, 0, 1];

        Assert.Null(FreehandArrowAnnotation.HeadAngle(x, y));
    }

    [Fact]
    public void Too_few_points_to_have_a_direction()
    {
        // Reachable: the method is public so the angle can be checked without a
        // canvas, and a caller with an empty gesture would otherwise index off
        // the end of the array.
        Assert.Null(FreehandArrowAnnotation.HeadAngle([], []));
        Assert.Null(FreehandArrowAnnotation.HeadAngle([5], [5]));

        // Mismatched lengths mean somebody built the arrays wrong; answering
        // "no direction" beats indexing into the shorter one.
        Assert.Null(FreehandArrowAnnotation.HeadAngle([0, 50], [0]));
    }

    [Fact]
    public void Bounds_leave_room_for_the_head()
    {
        var arrow = new FreehandArrowAnnotation(
            [(20, 20), (60, 20), (100, 20)], color: 0xFFB01030, strokeWidth: 3f);

        // The head reaches back and sideways from the tip, and Bounds is what
        // the session asks when deciding which monitors a preview belongs on: a
        // box that ends at the last point leaves the head unpainted next door.
        var reach = (int)Math.Ceiling(ArrowHead.Reach(3f, 80));

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
