using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// How see-through a window is.
///
/// Not Avalonia's Window.Opacity: on Win32 that leaves the window unlayered -
/// checked on a live pin, WS_EX_LAYERED was simply not set - and the window
/// stays as solid as it was. The alpha has to be asked of the compositor
/// directly, and that means the layered style plus one call.
/// </summary>
public static class WindowTransparency
{
    /// <param name="opacity">1 is solid, 0 is gone.</param>
    public static void Set(IntPtr window, double opacity)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var style = (long)NativeMethods.GetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE);
        var wanted = style | NativeMethods.WS_EX_LAYERED;

        if (wanted != style)
        {
            NativeMethods.SetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE, new IntPtr(wanted));
        }

        var alpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);

        NativeMethods.SetLayeredWindowAttributes(window, 0, alpha, NativeMethods.LWA_ALPHA);
    }
}
