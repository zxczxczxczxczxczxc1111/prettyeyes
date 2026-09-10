namespace PrettyEyes.Core.Geometry;

/// <summary>Which side of a monitor a shell bar is docked to.</summary>
public enum ShellEdge
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// The strips of a monitor that belong to the shell: the taskbar, and any
/// toolbar docked to an edge beside it.
///
/// The laser pointer's pane covers a whole monitor and owns the mouse while it
/// is on, which also means it owns the taskbar - and the tray icon is the one
/// place the mode can be switched off from without knowing a combination. So
/// the pane is given a hole over these, and clicks meant for the shell go to
/// the shell.
///
/// Everything here is in physical pixels of the virtual desktop, negative
/// coordinates included: a monitor placed left of the primary one has them, and
/// so does its taskbar.
/// </summary>
public static class ShellGap
{
    /// <summary>
    /// What to leave to the shell on one monitor.
    ///
    /// The work area is the honest source for a docked bar - Windows already
    /// subtracted it, whichever edge it is on and however many bars there are.
    /// An auto-hidden bar is the case it cannot answer: the work area then
    /// fills the monitor, because a hidden bar takes no room. Hence the last
    /// two arguments, which say where it would slide out and how thick it is.
    /// </summary>
    /// <param name="bounds">The whole monitor.</param>
    /// <param name="work">What the shell left for windows, when it said.</param>
    /// <param name="hidden">The edge an auto-hidden bar lives on, if one does.</param>
    /// <param name="thickness">How deep that bar is when it is out.</param>
    public static IReadOnlyList<CaptureRect> Of(
        CaptureRect bounds,
        CaptureRect? work,
        ShellEdge? hidden = null,
        int thickness = 0)
    {
        if (bounds.IsEmpty)
        {
            return [];
        }

        var gaps = new List<CaptureRect>(4);

        // A work area from another monitor overlaps this one in nothing, and
        // subtracting it would give away the whole pane. Ignored rather than
        // trusted: the caller pairing them up wrongly is a bug, and a pointer
        // that quietly stops covering a screen is a hard one to see.
        var usable = work is { IsEmpty: false } ? work.Value.Intersect(bounds) : bounds;

        if (usable.IsEmpty)
        {
            usable = bounds;
        }

        Keep(gaps, CaptureRect.FromPoints(bounds.X, bounds.Y, usable.X, bounds.Bottom));
        Keep(gaps, CaptureRect.FromPoints(usable.Right, bounds.Y, bounds.Right, bounds.Bottom));
        Keep(gaps, CaptureRect.FromPoints(bounds.X, bounds.Y, bounds.Right, usable.Y));
        Keep(gaps, CaptureRect.FromPoints(bounds.X, usable.Bottom, bounds.Right, bounds.Bottom));

        if (hidden is { } edge && thickness > 0)
        {
            Keep(gaps, Strip(bounds, edge, thickness));
        }

        return gaps;
    }

    private static void Keep(List<CaptureRect> gaps, CaptureRect gap)
    {
        if (!gap.IsEmpty)
        {
            gaps.Add(gap);
        }
    }

    /// <summary>
    /// Where a bar sits when it is out. Never deeper than the monitor: a bar
    /// reported as taller than the screen would take the whole pane, and a
    /// pointer with no pane left does not point at anything.
    /// </summary>
    private static CaptureRect Strip(CaptureRect bounds, ShellEdge edge, int thickness)
    {
        var across = edge is ShellEdge.Left or ShellEdge.Right;
        var depth = Math.Min(thickness, across ? bounds.Width : bounds.Height);

        return edge switch
        {
            ShellEdge.Left => new CaptureRect(bounds.X, bounds.Y, depth, bounds.Height),
            ShellEdge.Top => new CaptureRect(bounds.X, bounds.Y, bounds.Width, depth),
            ShellEdge.Right => new CaptureRect(bounds.Right - depth, bounds.Y, depth, bounds.Height),
            _ => new CaptureRect(bounds.X, bounds.Bottom - depth, bounds.Width, depth),
        };
    }
}
