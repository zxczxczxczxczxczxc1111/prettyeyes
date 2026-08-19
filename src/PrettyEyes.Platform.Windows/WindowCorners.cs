using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Rounds the corners of a borderless window through DWM. Doing it in the
/// compositor keeps the window opaque - an Avalonia window with a transparent
/// background and no decorations renders nothing at all on this build.
/// </summary>
public static class WindowCorners
{
    public static void Round(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var preference = NativeMethods.DwmCornerRound;
        NativeMethods.DwmSetWindowAttribute(
            window, NativeMethods.DwmWindowCornerPreference, ref preference, sizeof(int));
    }
}
