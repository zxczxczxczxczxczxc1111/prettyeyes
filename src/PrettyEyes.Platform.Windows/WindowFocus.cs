using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Remembers who had the keyboard before the overlay took it, and hands it
/// back at the right moment.
///
/// The right moment is earlier than it looks. While the overlay is up, the
/// screen shows a photograph taken before the keyboard was taken away, so in
/// that photograph the window behind is still drawn as the active one. Hide the
/// overlay first and the real window appears exactly as it is - inactive, drawn
/// dimmer - and only then, once Windows has handed the foreground back, does it
/// light up again. Bright, dim, bright: three states where the person did one
/// thing, and this is what "the tabs blink" turned out to be.
///
/// Handing the foreground back before anything is hidden puts the repaint under
/// the photograph, where nobody can see it.
/// </summary>
public static class WindowFocus
{
    /// <summary>Whoever has the keyboard right now, or zero.</summary>
    public static IntPtr Current => NativeMethods.GetForegroundWindow();

    /// <summary>
    /// Gives the keyboard back. Allowed without ceremony because we are the
    /// ones holding it: Windows only guards the other direction.
    /// </summary>
    public static void Restore(IntPtr window)
    {
        if (window == IntPtr.Zero || window == Current)
        {
            return;
        }

        NativeMethods.SetForegroundWindow(window);
    }
}
