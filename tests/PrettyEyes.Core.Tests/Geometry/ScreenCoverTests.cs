using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Tests.Geometry;

public class ScreenCoverTests
{
    private static readonly CaptureRect Monitor = new(0, 0, 2560, 1440);

    [Fact]
    public void A_window_the_size_of_the_monitor_covers_it()
    {
        // Borderless fullscreen: the usual shape of a game or a video player.
        Assert.True(ScreenCover.Covers(new CaptureRect(0, 0, 2560, 1440), Monitor));
    }

    [Fact]
    public void A_window_larger_than_the_monitor_covers_it()
    {
        // Some players hang a pixel over every edge.
        Assert.True(ScreenCover.Covers(new CaptureRect(-1, -1, 2562, 1442), Monitor));
    }

    [Fact]
    public void A_window_one_pixel_short_does_not()
    {
        // A maximised window leaves the taskbar visible, and a maximised window
        // is not something a pin has to get out of the way of.
        Assert.False(ScreenCover.Covers(new CaptureRect(0, 0, 2560, 1439), Monitor));
    }

    [Fact]
    public void A_window_of_the_right_size_in_the_wrong_place_does_not()
    {
        Assert.False(ScreenCover.Covers(new CaptureRect(40, 0, 2560, 1440), Monitor));
    }

    [Fact]
    public void Nothing_covers_nothing()
    {
        // A window with no size at all is a window that has just been created
        // or is on its way out.
        Assert.False(ScreenCover.Covers(CaptureRect.Empty, Monitor));
    }

    [Fact]
    public void A_second_monitor_is_covered_on_its_own_coordinates()
    {
        // The desktop starts at zero, the second screen does not, and the
        // window rectangle arrives in desktop coordinates.
        var second = new CaptureRect(2560, 0, 2560, 1440);

        Assert.True(ScreenCover.Covers(new CaptureRect(2560, 0, 2560, 1440), second));
        Assert.False(ScreenCover.Covers(new CaptureRect(0, 0, 2560, 1440), second));
    }
}
