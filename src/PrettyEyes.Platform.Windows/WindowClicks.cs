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
    /// And then the layer is given a value, which is the part that was missing.
    /// A window that has just been handed WS_EX_LAYERED has no layer contents
    /// until somebody says what they are, and until then Windows has nothing to
    /// compose and draws nothing at all. Live symptom, found 26.08.2026 and
    /// present in 1.3.0: the flash frame appeared on the first whole-monitor
    /// shot of a run and never again. It was a race, and the first shot won it
    /// because everything on that path was still cold.
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
