using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Puts a window back on top of everything.
///
/// Avalonia is told once, in the markup, that a pin is topmost, and that
/// should be the end of it. It was not: a pin was found sitting behind other
/// windows with the flag gone, and twenty minutes of watching every window
/// flag in the application never caught the moment it happened. Rather than
/// guess at the culprit, the pin now says it again after every show and every
/// move. The call costs nothing and the failure it prevents is a window the
/// user cannot find.
/// </summary>
public static class WindowOrder
{
    public static void KeepOnTop(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        // Nothing moves, nothing resizes, nothing steals the focus: only the
        // place in the z-order changes.
        NativeMethods.SetWindowPos(
            window,
            NativeMethods.HwndTopmost,
            0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }
}
