using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Cursor position straight from Win32. The process is per-monitor DPI aware,
/// so these are already physical pixels of the virtual desktop.
/// </summary>
public sealed class Win32PointerLocation : IPointerLocation
{
    public (int X, int Y) Current
    {
        get
        {
            if (!NativeMethods.GetCursorPos(out var point))
            {
                // No cursor position means no monitor to pick; the caller falls
                // back to the primary one.
                return (0, 0);
            }

            return (point.X, point.Y);
        }
    }
}
