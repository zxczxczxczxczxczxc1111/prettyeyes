using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class SelectionGripsTests
{
    private static readonly CaptureRect Selection = new(100, 100, 200, 100);
    private static readonly CaptureRect Frame = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(100, 100, SelectionGrip.TopLeft)]
    [InlineData(200, 100, SelectionGrip.Top)]
    [InlineData(300, 100, SelectionGrip.TopRight)]
    [InlineData(300, 150, SelectionGrip.Right)]
    [InlineData(300, 200, SelectionGrip.BottomRight)]
    [InlineData(200, 200, SelectionGrip.Bottom)]
    [InlineData(100, 200, SelectionGrip.BottomLeft)]
    [InlineData(100, 150, SelectionGrip.Left)]
    [InlineData(200, 150, SelectionGrip.Inside)]
    [InlineData(50, 50, SelectionGrip.None)]
    public void HitTest_finds_the_grip_under_the_point(int x, int y, SelectionGrip expected)
    {
        Assert.Equal(expected, SelectionGrips.HitTest(Selection, x, y, 8));
    }

    [Fact]
    public void HitTest_prefers_a_corner_over_an_edge_where_they_overlap()
    {
        // Four pixels inside the top-left corner: the corner and two edges all
        // match, and the corner has to win.
        Assert.Equal(SelectionGrip.TopLeft, SelectionGrips.HitTest(Selection, 104, 104, 8));
    }

    [Fact]
    public void Apply_inside_moves_the_whole_selection()
    {
        var moved = SelectionGrips.Apply(Selection, SelectionGrip.Inside, 30, -20, Frame);

        Assert.Equal(new CaptureRect(130, 80, 200, 100), moved);
    }

    [Fact]
    public void Apply_inside_keeps_the_selection_within_the_frame()
    {
        var moved = SelectionGrips.Apply(Selection, SelectionGrip.Inside, -500, -500, Frame);

        Assert.Equal(new CaptureRect(0, 0, 200, 100), moved);
    }

    [Fact]
    public void Apply_corner_moves_only_that_corner()
    {
        var resized = SelectionGrips.Apply(Selection, SelectionGrip.BottomRight, 50, 25, Frame);

        Assert.Equal(new CaptureRect(100, 100, 250, 125), resized);
    }

    [Fact]
    public void Apply_edge_moves_only_that_edge()
    {
        var resized = SelectionGrips.Apply(Selection, SelectionGrip.Left, 40, 999, Frame);

        // The vertical delta is ignored for a vertical edge.
        Assert.Equal(new CaptureRect(140, 100, 160, 100), resized);
    }

    [Fact]
    public void Apply_normalizes_when_an_edge_is_dragged_past_the_opposite_one()
    {
        var flipped = SelectionGrips.Apply(Selection, SelectionGrip.Left, 260, 0, Frame);

        Assert.Equal(new CaptureRect(300, 100, 60, 100), flipped);
    }

    [Fact]
    public void Apply_clamps_a_resize_to_the_frame()
    {
        var resized = SelectionGrips.Apply(Selection, SelectionGrip.Right, 5000, 0, Frame);

        Assert.Equal(new CaptureRect(100, 100, 1820, 100), resized);
    }
}
