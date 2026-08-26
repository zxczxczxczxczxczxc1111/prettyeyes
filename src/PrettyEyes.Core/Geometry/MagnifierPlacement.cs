namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Where the magnifier sits relative to the cursor.
///
/// Below and to the right, because that is where the hand is not: a
/// right-handed drag ends with the cursor moving down and right, and the
/// magnifier has to stay out of the way of what is being aimed at. It flips at
/// the edges rather than being pushed back, so it never covers the cursor.
/// </summary>
public static class MagnifierPlacement
{
    /// <summary>
    /// Chooses the box. The panel to keep clear of is optional and empty by
    /// default; pass one and the box moves to the other side of the cursor
    /// rather than hiding under it.
    /// </summary>
    public static CaptureRect Choose(
        int cursorX, int cursorY, CaptureRect monitor, int size, int gap, CaptureRect avoid = default)
    {
        var x = Flip(cursorX, monitor.X, monitor.Right, size, gap);
        var y = Flip(cursorY, monitor.Y, monitor.Bottom, size, gap);

        return new CaptureRect(x, Clear(x, y, cursorY, monitor, size, gap, avoid), size, size);
    }

    /// <summary>
    /// One axis: after the cursor by default, before it when there is no room
    /// left, and clamped when the monitor is smaller than the magnifier - which
    /// only happens on absurd resolutions, but a magnifier hanging off the
    /// screen would be worse.
    /// </summary>
    private static int Flip(int cursor, int start, int end, int size, int gap)
    {
        var after = cursor + gap;

        if (after + size > end)
        {
            after = cursor - gap - size;
        }

        return Math.Clamp(after, start, Math.Max(start, end - size));
    }

    /// <summary>
    /// Moves the box to the other side of the cursor when a panel is sitting
    /// where it wanted to be.
    ///
    /// Found live: hovering just above the toolbar put the magnifier under it.
    /// The overlay draws its panels after the canvas, so a covered magnifier is
    /// not dimmed, it is gone.
    ///
    /// Only the vertical is tried. The toolbar is a wide horizontal bar and
    /// stepping sideways would land on it again, while stepping up or down
    /// clears it in one move. Nothing happens when the other side is taken as
    /// well: swapping one covered magnifier for another would only read as a
    /// twitch. The size chip is not passed in today, so a magnifier can still
    /// land under it; that case was not reported and is smaller by the width of
    /// a single line of text.
    /// </summary>
    private static int Clear(
        int x, int y, int cursorY, CaptureRect monitor, int size, int gap, CaptureRect avoid)
    {
        if (avoid.IsEmpty || new CaptureRect(x, y, size, size).Intersect(avoid).IsEmpty)
        {
            return y;
        }

        var other = y > cursorY ? cursorY - gap - size : cursorY + gap;
        other = Math.Clamp(other, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - size));

        return new CaptureRect(x, other, size, size).Intersect(avoid).IsEmpty ? other : y;
    }
}
