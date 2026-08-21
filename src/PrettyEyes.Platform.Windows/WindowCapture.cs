using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Whether a window of ours shows up in captures of the screen.
///
/// Hiding it before a capture the way one would expect - Hide, capture, Show -
/// cannot work: the capture happens in the same turn of the UI thread, and the
/// compositor has not redrawn anything by then. The affinity is decided by DWM
/// itself, so it holds for both of our capture paths at once.
/// </summary>
public static class WindowCapture
{
    /// <summary>
    /// True to make the window invisible to screen capture. That also means
    /// invisible to a shared screen and to a recording, which is why this is
    /// off by default.
    /// </summary>
    public static void Exclude(IntPtr window, bool excluded)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        // Ignored on purpose: on Windows builds older than 2004 the call fails
        // and the window stays capturable, which is the harmless half of the
        // choice.
        NativeMethods.SetWindowDisplayAffinity(
            window,
            excluded ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);
    }
}
