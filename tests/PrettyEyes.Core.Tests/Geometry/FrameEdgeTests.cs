using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class FrameEdgeTests
{
    [Fact]
    public void A_selection_in_the_middle_of_the_screen_is_drawn_as_is()
    {
        Assert.Equal(500, FrameEdge.Bottom(selectionBottom: 500, usableBottom: 1400, monitorBottom: 1440));
    }

    [Fact]
    public void A_selection_under_the_taskbar_is_lifted_into_view()
    {
        // The overlay stops one pixel short of the monitor on purpose (a window
        // exactly the size of a monitor reads as a game and turns on Do Not
        // Disturb), and the shell paints its bar on top of what is left.
        // min(1440, min(1400, 1439) - 1) = 1399: the last row still visible
        // above the shell's bar. Not 1398, which is what this test asked for
        // in the first draft and would have failed on the very next step.
        Assert.Equal(1399, FrameEdge.Bottom(selectionBottom: 1440, usableBottom: 1400, monitorBottom: 1440));
    }

    [Fact]
    public void An_auto_hidden_taskbar_still_costs_one_pixel()
    {
        Assert.Equal(1438, FrameEdge.Bottom(selectionBottom: 1440, usableBottom: 1440, monitorBottom: 1440));
    }
}
