using PrettyEyes.Core.Capture;

namespace PrettyEyes.Core.Tests.Capture;

public class CaptureIntentTests
{
    [Fact]
    public void Nothing_open_means_a_new_screenshot()
    {
        Assert.Equal(
            CaptureRequest.Fresh,
            CaptureIntent.Decide(overlayOpen: false, overlayListening: false));
    }

    [Fact]
    public void An_overlay_that_still_has_the_keyboard_means_pick_again()
    {
        // The screen is already frozen and the user is looking at it. Taking a
        // second capture here would photograph the overlay's own frozen copy.
        Assert.Equal(
            CaptureRequest.Restart,
            CaptureIntent.Decide(overlayOpen: true, overlayListening: true));
    }

    [Fact]
    public void An_overlay_the_user_walked_away_from_is_not_reused()
    {
        // The bug this exists for: select a region, click another window in the
        // taskbar, press the hotkey again. The old code restarted an overlay
        // nobody could see or type into, and went on doing that until the
        // application was restarted. Whatever that overlay is holding is also a
        // photograph of a desktop that has moved on.
        Assert.Equal(
            CaptureRequest.Fresh,
            CaptureIntent.Decide(overlayOpen: true, overlayListening: false));
    }
}
