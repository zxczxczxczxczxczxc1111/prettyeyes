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
    public static CaptureRect Choose(int cursorX, int cursorY, CaptureRect monitor, int size, int gap)
    {
        var x = Flip(cursorX, monitor.X, monitor.Right, size, gap);
        var y = Flip(cursorY, monitor.Y, monitor.Bottom, size, gap);

        return new CaptureRect(x, y, size, size);
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
}
