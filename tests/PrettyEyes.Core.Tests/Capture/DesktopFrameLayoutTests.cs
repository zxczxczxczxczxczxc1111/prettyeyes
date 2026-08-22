using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Capture;

public class DesktopFrameLayoutTests
{
    private static MonitorInfo Monitor(string id, int x, int y, int width, int height) =>
        new(id, new CaptureRect(x, y, width, height), 1.0);

    private static DesktopLayout Desktop(params MonitorInfo[] monitors) => new(monitors);

    private static long Offset(DesktopFrameLayout frame, MonitorInfo monitor) =>
        frame.Placements.Single(p => p.Monitor.DeviceId == monitor.DeviceId).Offset;

    [Fact]
    public void Stride_and_size_cover_the_whole_desktop()
    {
        var frame = DesktopFrameLayout.For(Desktop(Monitor("one", 0, 0, 2560, 1440)));

        Assert.Equal(2560 * 4, frame.Stride);
        Assert.Equal((nuint)(2560L * 4 * 1440), frame.Size);
    }

    [Fact]
    public void Monitor_left_of_the_primary_one_starts_at_the_buffer_origin()
    {
        // A monitor to the left gives the desktop a negative left edge, and the
        // buffer still has to start at zero.
        var left = Monitor("left", -1920, 0, 1920, 1080);
        var main = Monitor("main", 0, 0, 2560, 1440);

        var frame = DesktopFrameLayout.For(Desktop(left, main));

        Assert.Equal(0, Offset(frame, left));
        Assert.Equal(1920L * 4, Offset(frame, main));
    }

    [Fact]
    public void Monitor_above_the_primary_one_is_offset_by_whole_rows()
    {
        var above = Monitor("above", 0, -1080, 1920, 1080);
        var main = Monitor("main", 0, 0, 2560, 1440);

        var frame = DesktopFrameLayout.For(Desktop(above, main));

        // The desktop is 2560 wide because the taller monitor sets the width.
        Assert.Equal(2560 * 4, frame.Stride);
        Assert.Equal(0, Offset(frame, above));
        Assert.Equal(1080L * 2560 * 4, Offset(frame, main));
    }

    [Fact]
    public void A_gap_between_monitors_asks_for_a_zeroed_buffer()
    {
        // Two monitors of different heights leave a corner of the bounding box
        // that nobody paints, and unpainted means somebody else's memory.
        var main = Monitor("main", 0, 0, 2560, 1440);
        var small = Monitor("small", 2560, 0, 1920, 1080);

        Assert.True(DesktopFrameLayout.For(Desktop(main, small)).NeedsZeroing);
    }

    [Fact]
    public void Monitors_that_tile_the_desktop_exactly_do_not_ask_for_zeroing()
    {
        // Clearing 28 MB is not free, and on the common side-by-side setup
        // every byte is overwritten anyway.
        var left = Monitor("left", 0, 0, 2560, 1440);
        var right = Monitor("right", 2560, 0, 2560, 1440);

        Assert.False(DesktopFrameLayout.For(Desktop(left, right)).NeedsZeroing);
    }

    [Fact]
    public void Overlapping_monitors_still_ask_for_a_zeroed_buffer()
    {
        // The old code compared the sum of monitor areas against the desktop
        // area, so an overlap bigger than the hole next door added up to "no
        // gaps" and left the hole full of whatever was there before.
        //
        // Here the areas sum to 2 180 000 against a desktop of 2 090 000, so
        // the old rule saw no gap, while the bottom right corner of 100x100 is
        // painted by nobody.
        var one = Monitor("one", 0, 0, 1000, 1000);
        var two = Monitor("two", 900, 0, 1000, 1000);
        var three = Monitor("three", 0, 1000, 1800, 100);

        Assert.True(DesktopFrameLayout.For(Desktop(one, two, three)).NeedsZeroing);
    }

    [Fact]
    public void A_desktop_too_wide_for_an_int_stride_is_refused_by_name()
    {
        var absurd = Monitor("absurd", 0, 0, int.MaxValue / 3, 4);

        var error = Assert.Throws<InvalidOperationException>(
            () => DesktopFrameLayout.For(Desktop(absurd)));

        Assert.Contains("too wide", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_desktop_with_no_area_is_refused_by_name()
    {
        var empty = Monitor("empty", 0, 0, 0, 0);

        var error = Assert.Throws<InvalidOperationException>(
            () => DesktopFrameLayout.For(Desktop(empty)));

        Assert.Contains("non-positive", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
