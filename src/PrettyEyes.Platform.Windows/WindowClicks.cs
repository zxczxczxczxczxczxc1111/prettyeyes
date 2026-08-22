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
    }
}
