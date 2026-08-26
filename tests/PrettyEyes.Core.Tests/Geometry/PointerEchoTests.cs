using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class PointerEchoTests
{
    private static PointerEcho Sample() =>
        new(120, 44, Modifiers: 0, Mode: 1, Dragging: false, new CaptureRect(0, 0, 800, 600));

    [Fact]
    public void The_same_state_repeats_itself()
    {
        Assert.Equal(Sample(), Sample());
    }

    [Fact]
    public void A_new_pixel_is_not_a_repeat()
    {
        Assert.NotEqual(Sample(), Sample() with { X = 121 });
        Assert.NotEqual(Sample(), Sample() with { Y = 45 });
    }

    [Fact]
    public void Pressing_a_modifier_without_moving_is_not_a_repeat()
    {
        // Ctrl over a stamped glyph turns the cursor into a carry. Windows
        // sends no move for a modifier, but it sends plenty for a shaking
        // hand, and those land on the same physical pixel.
        Assert.NotEqual(Sample(), Sample() with { Modifiers = 2 });
    }

    [Fact]
    public void Letting_go_of_the_button_is_not_a_repeat()
    {
        // Released without moving: the toolbar appears under the cursor and
        // the magnifier has to get out from under it.
        Assert.NotEqual(Sample(), Sample() with { Dragging = true });
    }

    [Fact]
    public void Changing_mode_is_not_a_repeat()
    {
        Assert.NotEqual(Sample(), Sample() with { Mode = 2 });
    }

    [Fact]
    public void Nudging_the_frame_under_a_still_cursor_is_not_a_repeat()
    {
        // Arrow keys move the selection while the pointer stays put. The
        // cursor must stop claiming a grip that is no longer under it.
        Assert.NotEqual(Sample(), Sample() with { Selection = new CaptureRect(4, 0, 800, 600) });
    }
}
