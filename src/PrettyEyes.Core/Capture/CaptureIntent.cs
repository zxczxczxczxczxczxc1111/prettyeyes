namespace PrettyEyes.Core.Capture;

/// <summary>What pressing the capture hotkey means at this moment.</summary>
public enum CaptureRequest
{
    /// <summary>Take the screen again and open a new overlay over it.</summary>
    Fresh,

    /// <summary>Keep the frozen screen, drop the selection, let them pick again.</summary>
    Restart,
}

/// <summary>
/// Reads the hotkey.
///
/// It used to mean "restart" whenever an overlay existed, and existing is not
/// the same as being usable. An overlay that lost the keyboard - the taskbar,
/// alt-tab, a click on another window - stays in the field but hears nothing:
/// Escape cannot reach it, so it never closes, and every press after that
/// restarted a window nobody could see. The way out was to restart the whole
/// application, which is a thing no screenshot tool should ever ask for.
/// </summary>
public static class CaptureIntent
{
    /// <param name="overlayOpen">An overlay session exists.</param>
    /// <param name="overlayListening">One of its windows has the keyboard.</param>
    public static CaptureRequest Decide(bool overlayOpen, bool overlayListening) =>
        overlayOpen && overlayListening
            ? CaptureRequest.Restart

            // Not reused on purpose, even though a frozen frame is sitting
            // right there: the person went somewhere else and came back, and
            // what that overlay holds is a photograph of a desktop that has
            // moved on since.
            : CaptureRequest.Fresh;
}
