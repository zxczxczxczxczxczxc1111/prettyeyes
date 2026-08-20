namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Where a floating panel goes next to the selection, vertically.
///
/// Lives here rather than in the window because the answer is arithmetic and
/// the window is the one place where arithmetic cannot be tested: the case
/// that matters - a selection covering the whole monitor - needs a screen,
/// two monitors and a taskbar to reproduce by hand.
///
/// Everything is in logical pixels, top-down, relative to the window.
/// </summary>
public static class PanelPlacement
{
    /// <summary>
    /// Under the selection, above it when there is no room below, inside it
    /// when there is no room either way.
    ///
    /// <paramref name="limitBottom"/> is the last row the panel may occupy -
    /// the top of the taskbar, not the bottom of the screen. A panel that ends
    /// up under the taskbar is invisible: the overlay window stops one pixel
    /// short of the monitor on purpose, so the shell keeps its bar on top of
    /// it.
    /// </summary>
    public static double Vertical(
        double selectionTop,
        double selectionBottom,
        double panelHeight,
        double gap,
        double limitTop,
        double limitBottom)
    {
        var below = selectionBottom + gap;

        if (below + panelHeight <= limitBottom)
        {
            return below;
        }

        var above = selectionTop - gap - panelHeight;

        if (above >= limitTop)
        {
            return above;
        }

        // Neither side has room, so the panel goes inside the selection,
        // pinned to whichever edge is actually reachable.
        var inside = Math.Min(selectionBottom, limitBottom) - gap - panelHeight;

        return Math.Max(limitTop, inside);
    }
}
