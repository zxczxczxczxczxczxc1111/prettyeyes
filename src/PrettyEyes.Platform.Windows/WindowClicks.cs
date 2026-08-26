using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Windows that are looked at rather than used.
/// </summary>
public static class WindowClicks
{
    /// <summary>
    /// The mouse stops seeing this window: clicks land on whatever is behind
    /// it. WS_EX_TRANSPARENT only works on a layered window, so the layer is
    /// asked for at the same time.
    ///
    /// And then the layer is given a value. A window handed WS_EX_LAYERED has no
    /// layer contents until somebody says what they are, and Windows composes
    /// what it is told about.
    ///
    /// Added 26.08.2026 while chasing a flash frame that showed up on the first
    /// whole-monitor shot of a run and rarely after. It helped and it was not
    /// the cause: the cause was the UI thread being busy decorating the
    /// screenshot, so the window could not paint until after it had been told
    /// to close. Kept because a layered window with an undefined layer is a
    /// loaded gun either way, not because it fixed that.
    /// </summary>
    public static void PassThrough(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var style = (long)NativeMethods.GetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE);

        NativeMethods.SetWindowLongPtr(
            window,
            NativeMethods.GWL_EXSTYLE,
            new IntPtr(style | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED));

        // Fully opaque, so the per-pixel alpha the window paints with is what
        // decides what is seen. This says "the layer is solid", not "the window
        // is solid": the frame is still a border around nothing.
        NativeMethods.SetLayeredWindowAttributes(window, 0, 255, NativeMethods.LWA_ALPHA);
    }
}
