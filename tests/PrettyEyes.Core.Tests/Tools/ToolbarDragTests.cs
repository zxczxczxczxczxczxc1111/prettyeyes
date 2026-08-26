using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class ToolbarDragTests
{
    [Fact]
    public void A_frame_pulled_by_a_grip_keeps_the_panel_with_it()
    {
        // Leaving it behind and teleporting it at the end of the gesture reads
        // as a glitch, which is why it followed in the first place.
        Assert.True(ToolbarDrag.FollowsFrame(toolbarShown: true, freshDrag: false));
    }

    [Fact]
    public void A_frame_drawn_from_scratch_leaves_the_panel_out_of_it()
    {
        // The second selection used to grow a panel beside it the whole way
        // down the drag. The first one never did, and the first one is right:
        // the panel belongs to the finished gesture.
        Assert.False(ToolbarDrag.FollowsFrame(toolbarShown: true, freshDrag: true));
    }

    [Fact]
    public void Before_the_panel_has_ever_appeared_there_is_nothing_to_follow()
    {
        Assert.False(ToolbarDrag.FollowsFrame(toolbarShown: false, freshDrag: false));
        Assert.False(ToolbarDrag.FollowsFrame(toolbarShown: false, freshDrag: true));
    }
}
