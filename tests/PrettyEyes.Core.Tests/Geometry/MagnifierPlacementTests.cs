using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class MagnifierPlacementTests
{
    private static readonly CaptureRect Monitor = new(0, 0, 2560, 1440);

    private const int Size = 132;
    private const int Gap = 24;

    private static CaptureRect Place(int x, int y) =>
        MagnifierPlacement.Choose(x, y, Monitor, Size, Gap);

    [Fact]
    public void In_the_open_it_sits_below_and_to_the_right_of_the_cursor()
    {
        var placed = Place(1000, 700);

        Assert.Equal(1000 + Gap, placed.X);
        Assert.Equal(700 + Gap, placed.Y);
        Assert.Equal(Size, placed.Width);
        Assert.Equal(Size, placed.Height);
    }

    [Fact]
    public void Near_the_right_edge_it_flips_to_the_left()
    {
        var placed = Place(2550, 700);

        Assert.Equal(2550 - Gap - Size, placed.X);
        Assert.Equal(700 + Gap, placed.Y);
    }

    [Fact]
    public void Near_the_bottom_edge_it_flips_upwards()
    {
        var placed = Place(1000, 1430);

        Assert.Equal(1000 + Gap, placed.X);
        Assert.Equal(1430 - Gap - Size, placed.Y);
    }

    [Fact]
    public void In_the_bottom_right_corner_it_flips_both_ways()
    {
        var placed = Place(2550, 1430);

        Assert.Equal(2550 - Gap - Size, placed.X);
        Assert.Equal(1430 - Gap - Size, placed.Y);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2559, 1439)]
    [InlineData(5, 1435)]
    [InlineData(2555, 3)]
    [InlineData(1280, 720)]
    public void It_never_hangs_off_the_monitor(int x, int y)
    {
        var placed = Place(x, y);

        Assert.True(placed.X >= Monitor.X, "левый край");
        Assert.True(placed.Y >= Monitor.Y, "верхний край");
        Assert.True(placed.Right <= Monitor.Right, "правый край");
        Assert.True(placed.Bottom <= Monitor.Bottom, "нижний край");
    }

    [Fact]
    public void On_a_monitor_left_of_the_primary_the_coordinates_stay_negative()
    {
        var left = new CaptureRect(-2560, 0, 2560, 1440);

        var placed = MagnifierPlacement.Choose(-1300, 700, left, Size, Gap);

        Assert.Equal(-1300 + Gap, placed.X);
        Assert.True(placed.Right <= left.Right);
    }

    [Fact]
    public void A_monitor_smaller_than_the_magnifier_still_gets_a_placement_inside_it()
    {
        var tiny = new CaptureRect(0, 0, 100, 100);

        var placed = MagnifierPlacement.Choose(50, 50, tiny, Size, Gap);

        Assert.Equal(0, placed.X);
        Assert.Equal(0, placed.Y);
    }

    [Fact]
    public void A_panel_below_the_cursor_pushes_it_up()
    {
        // Reported live 26.08.2026: hovering just above the toolbar put the
        // magnifier underneath it. The toolbar is a later sibling in the same
        // panel, so it paints on top and the magnifier is simply gone.
        var toolbar = new CaptureRect(900, 740, 400, 44);

        var placed = MagnifierPlacement.Choose(1000, 700, Monitor, Size, Gap, toolbar);

        Assert.Equal(700 - Gap - Size, placed.Y);
        Assert.Equal(1000 + Gap, placed.X);
    }

    [Fact]
    public void A_panel_it_does_not_touch_changes_nothing()
    {
        var elsewhere = new CaptureRect(100, 100, 400, 44);

        var placed = MagnifierPlacement.Choose(1000, 700, Monitor, Size, Gap, elsewhere);

        Assert.Equal(700 + Gap, placed.Y);
    }

    [Fact]
    public void With_no_panel_given_it_behaves_as_before()
    {
        Assert.Equal(Place(1000, 700), MagnifierPlacement.Choose(1000, 700, Monitor, Size, Gap));
    }

    [Fact]
    public void A_panel_on_both_sides_leaves_it_where_it_was()
    {
        // Nowhere better to go: moving it would trade one covered magnifier for
        // another, and the flip would read as a twitch.
        var everywhere = new CaptureRect(0, 0, 2560, 1440);

        var placed = MagnifierPlacement.Choose(1000, 700, Monitor, Size, Gap, everywhere);

        Assert.Equal(700 + Gap, placed.Y);
    }

    [Fact]
    public void Pushed_up_it_still_stays_on_the_monitor()
    {
        // Near the top edge there is no room above, so the clamp has to win
        // over the panel: off-screen is worse than covered.
        var toolbar = new CaptureRect(900, 20, 400, 44);

        var placed = MagnifierPlacement.Choose(1000, 10, Monitor, Size, Gap, toolbar);

        Assert.True(placed.Y >= Monitor.Y);
        Assert.True(placed.Bottom <= Monitor.Bottom);
    }
}
