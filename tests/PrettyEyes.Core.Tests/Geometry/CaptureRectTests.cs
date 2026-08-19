using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class CaptureRectTests
{
    [Theory]
    // dragged right-down
    [InlineData(10, 10, 40, 30, 10, 10, 30, 20)]
    // dragged left-up: must normalize
    [InlineData(40, 30, 10, 10, 10, 10, 30, 20)]
    // dragged left-down
    [InlineData(40, 10, 10, 30, 10, 10, 30, 20)]
    // dragged right-up
    [InlineData(10, 30, 40, 10, 10, 10, 30, 20)]
    // negative coordinates: a monitor left of the primary one
    [InlineData(-100, 10, -40, 50, -100, 10, 60, 40)]
    public void FromPoints_normalizes_any_drag_direction(
        int x1, int y1, int x2, int y2,
        int expectedX, int expectedY, int expectedW, int expectedH)
    {
        var rect = CaptureRect.FromPoints(x1, y1, x2, y2);

        Assert.Equal(expectedX, rect.X);
        Assert.Equal(expectedY, rect.Y);
        Assert.Equal(expectedW, rect.Width);
        Assert.Equal(expectedH, rect.Height);
    }

    [Fact]
    public void FromPoints_with_same_point_gives_empty_rect()
    {
        Assert.True(CaptureRect.FromPoints(15, 15, 15, 15).IsEmpty);
    }

    [Fact]
    public void Intersect_returns_overlapping_part()
    {
        var a = new CaptureRect(0, 0, 100, 100);
        var b = new CaptureRect(50, 50, 100, 100);

        Assert.Equal(new CaptureRect(50, 50, 50, 50), a.Intersect(b));
    }

    [Fact]
    public void Intersect_without_overlap_returns_empty()
    {
        var a = new CaptureRect(0, 0, 10, 10);
        var b = new CaptureRect(50, 50, 10, 10);

        Assert.True(a.Intersect(b).IsEmpty);
    }

    [Fact]
    public void Intersect_clamps_a_selection_reaching_outside_the_frame()
    {
        var frame = new CaptureRect(0, 0, 1920, 1080);
        var selection = new CaptureRect(1800, 1000, 400, 400);

        Assert.Equal(new CaptureRect(1800, 1000, 120, 80), selection.Intersect(frame));
    }

    [Theory]
    // inside stays put
    [InlineData(15, 15, 15, 15)]
    // outside on every side snaps to the last pixel that belongs to the rect
    [InlineData(5, 15, 10, 15)]
    [InlineData(99, 15, 29, 15)]
    [InlineData(15, 5, 15, 10)]
    [InlineData(15, 99, 15, 29)]
    public void ClampPoint_keeps_a_point_inside(int x, int y, int expectedX, int expectedY)
    {
        var rect = new CaptureRect(10, 10, 20, 20);

        Assert.Equal((expectedX, expectedY), rect.ClampPoint(x, y));
    }

    [Fact]
    public void Contains_is_half_open_on_the_right_and_bottom_edges()
    {
        var rect = new CaptureRect(10, 10, 20, 20);

        Assert.True(rect.Contains(10, 10));
        Assert.True(rect.Contains(29, 29));
        // Right and bottom edges belong to the neighbouring monitor.
        Assert.False(rect.Contains(30, 15));
        Assert.False(rect.Contains(15, 30));
    }
}
