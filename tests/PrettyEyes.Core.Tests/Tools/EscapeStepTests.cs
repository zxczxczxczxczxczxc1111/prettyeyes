using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class EscapeStepTests
{
    [Fact]
    public void A_default_tool_over_an_empty_canvas_is_not_worth_a_press()
    {
        // Nobody picked this one: the default armed itself the moment the frame
        // settled. Taking it back was a step the user never asked for, and it
        // cost a second Escape on every single capture.
        Assert.False(EscapeStep.ClearsTool(pickedByHand: false, hasAnnotations: false));
    }

    [Fact]
    public void A_tool_picked_by_hand_is_taken_back_first()
    {
        // With a tool armed the frame cannot be moved at all, and this is how
        // people put it down. Closing the capture instead would throw away a
        // selection somebody had just finished making.
        Assert.True(EscapeStep.ClearsTool(pickedByHand: true, hasAnnotations: false));
    }

    [Fact]
    public void Anything_already_drawn_is_worth_more_than_the_press()
    {
        // Even the default tool stays reachable here: closing outright would
        // take the drawing with it.
        Assert.True(EscapeStep.ClearsTool(pickedByHand: false, hasAnnotations: true));
        Assert.True(EscapeStep.ClearsTool(pickedByHand: true, hasAnnotations: true));
    }
}
