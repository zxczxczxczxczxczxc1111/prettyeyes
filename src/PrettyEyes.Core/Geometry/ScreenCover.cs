namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Whether a window has a whole screen to itself.
///
/// A pinned screenshot is always on top, and always on top is exactly wrong in
/// front of a game or a video playing full screen: Windows takes the fullscreen
/// application out of its exclusive mode to make room for the topmost window,
/// the application asks for it back, and the two of them do that to each other
/// until somebody closes something.
/// </summary>
public static class ScreenCover
{
    /// <summary>
    /// True when the window leaves nothing of the monitor showing.
    ///
    /// Edge to edge and nothing less: a maximised window stops one taskbar
    /// short of this, and a maximised window is not something anybody needs a
    /// pin to get out of the way of.
    /// </summary>
    public static bool Covers(CaptureRect window, CaptureRect monitor) =>
        !window.IsEmpty
        && !monitor.IsEmpty
        && window.X <= monitor.X
        && window.Y <= monitor.Y
        && window.Right >= monitor.Right
        && window.Bottom >= monitor.Bottom;
}
