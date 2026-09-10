using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

/// <summary>
/// Which strips of a monitor belong to the shell rather than to us.
///
/// A pane that covers a whole monitor and owns the mouse also owns the taskbar,
/// and the tray icon is the one place the pane can be turned off from without a
/// combination. So the pane gets a hole, and this works out where.
///
/// Coordinates are physical pixels of the virtual desktop, so a monitor to the
/// left of the primary one has negative ones and the answers have to as well.
/// </summary>
public class ShellGapTests
{
    private static readonly CaptureRect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void A_monitor_with_no_work_area_reported_gives_nothing_away()
    {
        Assert.Empty(ShellGap.Of(Screen, work: null));
    }

    [Fact]
    public void A_work_area_that_fills_the_monitor_gives_nothing_away()
    {
        Assert.Empty(ShellGap.Of(Screen, Screen));
    }

    [Fact]
    public void A_taskbar_at_the_bottom_is_the_strip_below_the_work_area()
    {
        var gaps = ShellGap.Of(Screen, new CaptureRect(0, 0, 1920, 1032));

        Assert.Equal(new CaptureRect(0, 1032, 1920, 48), Assert.Single(gaps));
    }

    [Fact]
    public void A_taskbar_on_the_left_is_the_strip_beside_the_work_area()
    {
        var gaps = ShellGap.Of(Screen, new CaptureRect(62, 0, 1858, 1080));

        Assert.Equal(new CaptureRect(0, 0, 62, 1080), Assert.Single(gaps));
    }

    /// <summary>
    /// A taskbar and a docked toolbar on another edge are two strips, and both
    /// of them take clicks.
    /// </summary>
    [Fact]
    public void Two_bars_on_two_edges_are_two_strips()
    {
        var gaps = ShellGap.Of(Screen, new CaptureRect(0, 40, 1880, 1040));

        Assert.Equal(2, gaps.Count);
        Assert.Contains(new CaptureRect(1880, 0, 40, 1080), gaps);
        Assert.Contains(new CaptureRect(0, 0, 1920, 40), gaps);
    }

    /// <summary>
    /// A monitor left of the primary one starts at a negative x, and so does
    /// its taskbar. Worked out in the monitor's own coordinates, the hole would
    /// be punched on the wrong screen.
    /// </summary>
    [Fact]
    public void A_monitor_left_of_the_primary_one_answers_in_desktop_coordinates()
    {
        var screen = new CaptureRect(-1920, -200, 1920, 1080);
        var gaps = ShellGap.Of(screen, new CaptureRect(-1920, -200, 1920, 1032));

        Assert.Equal(new CaptureRect(-1920, 832, 1920, 48), Assert.Single(gaps));
    }

    /// <summary>
    /// An auto-hidden taskbar leaves the work area filling the whole monitor,
    /// so there is nothing to subtract and the pane covers the edge the bar
    /// slides out of. Then it never slides out, because the pane has the mouse.
    /// </summary>
    [Fact]
    public void An_auto_hidden_bar_still_gets_the_strip_it_would_occupy()
    {
        var gaps = ShellGap.Of(Screen, Screen, ShellEdge.Bottom, thickness: 48);

        Assert.Equal(new CaptureRect(0, 1032, 1920, 48), Assert.Single(gaps));
    }

    [Fact]
    public void An_auto_hidden_bar_on_the_right_gets_a_strip_down_that_side()
    {
        var gaps = ShellGap.Of(Screen, Screen, ShellEdge.Right, thickness: 62);

        Assert.Equal(new CaptureRect(1858, 0, 62, 1080), Assert.Single(gaps));
    }

    [Fact]
    public void An_auto_hidden_bar_of_no_thickness_is_not_a_bar()
    {
        Assert.Empty(ShellGap.Of(Screen, Screen, ShellEdge.Bottom, thickness: 0));
    }

    /// <summary>
    /// A bar reported as thicker than the screen would punch the pane out
    /// completely, and a pane with no pixels left is a pointer that does not
    /// point.
    /// </summary>
    [Fact]
    public void A_bar_thicker_than_the_monitor_is_cut_down_to_it()
    {
        var gaps = ShellGap.Of(Screen, Screen, ShellEdge.Top, thickness: 5000);

        Assert.Equal(new CaptureRect(0, 0, 1920, 1080), Assert.Single(gaps));
    }

    /// <summary>
    /// The work area of one monitor says nothing about another. Passed the
    /// wrong one, the whole pane would be given away rather than a strip of it.
    /// </summary>
    [Fact]
    public void A_work_area_belonging_to_another_monitor_is_ignored()
    {
        Assert.Empty(ShellGap.Of(Screen, new CaptureRect(-1920, 0, 1920, 1032)));
    }

    [Fact]
    public void An_empty_monitor_has_nothing_to_give_away()
    {
        Assert.Empty(ShellGap.Of(CaptureRect.Empty, null, ShellEdge.Bottom, thickness: 48));
    }
}
