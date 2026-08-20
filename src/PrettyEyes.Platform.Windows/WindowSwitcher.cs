using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Keeps windows out of Alt+Tab.
///
/// ShowInTaskbar only removes the taskbar button; the switcher lists every
/// visible top-level window that is not a tool window, which is how a
/// one-pixel invisible host window ends up in there as a blank card called
/// "Window". WS_EX_TOOLWINDOW is the only flag the switcher honours.
/// </summary>
public static class WindowSwitcher
{
    public static void Hide(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var style = (long)NativeMethods.GetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE);
        var wanted = (style | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;

        if (wanted == style)
        {
            return;
        }

        NativeMethods.SetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE, new IntPtr(wanted));
    }
}
