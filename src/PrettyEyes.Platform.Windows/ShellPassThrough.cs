using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Cuts the taskbar out of a window that covers a whole monitor, so clicks
/// meant for the shell reach the shell.
///
/// The laser pointer's pane owns the mouse while the mode is on - that is what
/// buys it a usable frame rate, measured, see LaserWindow - and a pane over the
/// whole screen therefore also swallows the taskbar. Which would be a detail,
/// except that the tray icon is the one place the mode can be switched off from
/// by somebody who has not assigned a combination to it. On means stuck on.
///
/// A window region rather than input transparency: a region takes the pixels
/// out of the window entirely, so the hit test answers with whatever is
/// underneath and nothing is drawn there either. WS_EX_LAYERED, which is what
/// input transparency across processes actually costs, drops the pane from
/// forty-eight frames a second to ten.
/// </summary>
public static class ShellPassThrough
{
    /// <summary>
    /// Leaves the shell's own strips of this monitor out of the window. Called
    /// after the window is up and its handle exists.
    /// </summary>
    public static void Keep(IntPtr window, CaptureRect bounds, CaptureRect? work)
    {
        if (window == IntPtr.Zero || bounds.IsEmpty)
        {
            return;
        }

        var (edge, thickness) = Hidden();
        var gaps = ShellGap.Of(bounds, work, edge, thickness);

        if (gaps.Count == 0)
        {
            // Nothing to cut out, and a window with no region is not the same
            // as one whose region is everything: clearing it is how the pane
            // gets its whole monitor back if the taskbar moves away.
            NativeMethods.SetWindowRgn(window, IntPtr.Zero, true);

            return;
        }

        // Window coordinates, which start at the window's own top left corner
        // and are physical pixels whatever the monitor is scaled to.
        var region = NativeMethods.CreateRectRgn(0, 0, bounds.Width, bounds.Height);

        foreach (var gap in gaps)
        {
            var hole = NativeMethods.CreateRectRgn(
                gap.X - bounds.X,
                gap.Y - bounds.Y,
                gap.Right - bounds.X,
                gap.Bottom - bounds.Y);

            NativeMethods.CombineRgn(region, region, hole, NativeMethods.RGN_DIFF);
            NativeMethods.DeleteObject(hole);
        }

        // The window takes the region over and frees it itself, so it is not
        // deleted here - unless the call failed, in which case nobody owns it.
        if (NativeMethods.SetWindowRgn(window, region, true) == 0)
        {
            NativeMethods.DeleteObject(region);
            Log.Default.Info("не удалось вырезать панель задач из окна указки");
        }
    }

    /// <summary>
    /// Where an auto-hidden taskbar would be if it were out, and how deep.
    ///
    /// The work area cannot answer this: a hidden bar takes no room, so the
    /// work area fills the monitor and there is nothing to subtract. The shell
    /// still knows where the bar lives, and asking is one message.
    ///
    /// Only the primary bar is reported. The secondary ones follow it - same
    /// edge, same thickness - so the answer is used on every monitor.
    /// </summary>
    private static (ShellEdge? Edge, int Thickness) Hidden()
    {
        var data = new NativeMethods.AppBarData
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.AppBarData>(),
        };

        var state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data).ToInt64();

        if ((state & NativeMethods.ABS_AUTOHIDE) == 0)
        {
            return (null, 0);
        }

        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data) == IntPtr.Zero)
        {
            return (null, 0);
        }

        var across = data.uEdge is NativeMethods.ABE_LEFT or NativeMethods.ABE_RIGHT;
        var thickness = across
            ? data.rc.Right - data.rc.Left
            : data.rc.Bottom - data.rc.Top;

        var edge = data.uEdge switch
        {
            NativeMethods.ABE_LEFT => ShellEdge.Left,
            NativeMethods.ABE_TOP => ShellEdge.Top,
            NativeMethods.ABE_RIGHT => ShellEdge.Right,
            _ => ShellEdge.Bottom,
        };

        return (edge, thickness);
    }
}
