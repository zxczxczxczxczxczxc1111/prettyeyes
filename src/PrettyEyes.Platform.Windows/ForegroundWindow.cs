using System.Runtime.InteropServices;
using System.Text;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Answers one question: does somebody else own a whole screen right now.
///
/// Asked by the pinned windows, which are always on top and have to stop being
/// that in front of a game or a video playing full screen. Two things happen
/// otherwise, and both were seen: the fullscreen application flickers as
/// Windows drops it out of exclusive mode to make room for the topmost window
/// and it asks for the mode back, and the pin ends up behind it for good
/// because the fight is settled by whoever gave up last.
/// </summary>
public static class ForegroundWindow
{
    /// <summary>
    /// The desktop itself. It is the size of a screen by definition, and it is
    /// in front whenever nothing else is.
    /// </summary>
    private static readonly string[] Shell = ["Progman", "WorkerW", "Shell_TrayWnd"];

    /// <summary>
    /// True when the window in front belongs to another application and covers
    /// its monitor edge to edge.
    ///
    /// Our own windows never count. The capture overlay is a fullscreen window
    /// of ours, and a pin hiding from it would be a pin hiding from the
    /// screenshot it is part of.
    /// </summary>
    public static bool CoversAScreen()
    {
        var window = NativeMethods.GetForegroundWindow();

        if (window == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(window, out var owner);

        if (owner == (uint)Environment.ProcessId)
        {
            return false;
        }

        if (IsShell(window))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(window, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST);

        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MonitorInfoEx
        {
            cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        // rcMonitor and not rcWork: the work area stops at the taskbar, and a
        // window that merely reaches the taskbar is a maximised window.
        return ScreenCover.Covers(Rectangle(rect), Rectangle(info.rcMonitor));
    }

    private static bool IsShell(IntPtr window)
    {
        var name = new StringBuilder(64);

        return NativeMethods.GetClassName(window, name, name.Capacity) > 0
            && Array.IndexOf(Shell, name.ToString()) >= 0;
    }

    private static CaptureRect Rectangle(NativeMethods.Rect rect) => new(
        rect.Left,
        rect.Top,
        rect.Right - rect.Left,
        rect.Bottom - rect.Top);
}
