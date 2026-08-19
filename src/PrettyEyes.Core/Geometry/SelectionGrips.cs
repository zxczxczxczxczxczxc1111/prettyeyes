namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Hit-testing and resizing of the selection. Lives in Core so the arithmetic
/// is covered by tests instead of being rediscovered by hand in the overlay.
/// </summary>
public static class SelectionGrips
{
    public static SelectionGrip HitTest(CaptureRect selection, int x, int y, int reach)
    {
        if (selection.IsEmpty)
        {
            return SelectionGrip.None;
        }

        var nearLeft = Math.Abs(x - selection.X) <= reach;
        var nearRight = Math.Abs(x - selection.Right) <= reach;
        var nearTop = Math.Abs(y - selection.Y) <= reach;
        var nearBottom = Math.Abs(y - selection.Bottom) <= reach;

        var withinX = x >= selection.X - reach && x <= selection.Right + reach;
        var withinY = y >= selection.Y - reach && y <= selection.Bottom + reach;

        // Corners first: next to a corner every neighbouring edge matches too.
        if (nearLeft && nearTop) return SelectionGrip.TopLeft;
        if (nearRight && nearTop) return SelectionGrip.TopRight;
        if (nearRight && nearBottom) return SelectionGrip.BottomRight;
        if (nearLeft && nearBottom) return SelectionGrip.BottomLeft;

        if (nearTop && withinX) return SelectionGrip.Top;
        if (nearBottom && withinX) return SelectionGrip.Bottom;
        if (nearLeft && withinY) return SelectionGrip.Left;
        if (nearRight && withinY) return SelectionGrip.Right;

        return selection.Contains(x, y) ? SelectionGrip.Inside : SelectionGrip.None;
    }

    public static CaptureRect Apply(CaptureRect selection, SelectionGrip grip, int dx, int dy, CaptureRect frame)
    {
        if (grip == SelectionGrip.Inside)
        {
            var x = Math.Clamp(selection.X + dx, frame.X, frame.Right - selection.Width);
            var y = Math.Clamp(selection.Y + dy, frame.Y, frame.Bottom - selection.Height);

            return new CaptureRect(x, y, selection.Width, selection.Height);
        }

        var left = selection.X;
        var top = selection.Y;
        var right = selection.Right;
        var bottom = selection.Bottom;

        if (grip is SelectionGrip.TopLeft or SelectionGrip.Left or SelectionGrip.BottomLeft)
        {
            left = Math.Clamp(left + dx, frame.X, frame.Right);
        }

        if (grip is SelectionGrip.TopRight or SelectionGrip.Right or SelectionGrip.BottomRight)
        {
            right = Math.Clamp(right + dx, frame.X, frame.Right);
        }

        if (grip is SelectionGrip.TopLeft or SelectionGrip.Top or SelectionGrip.TopRight)
        {
            top = Math.Clamp(top + dy, frame.Y, frame.Bottom);
        }

        if (grip is SelectionGrip.BottomLeft or SelectionGrip.Bottom or SelectionGrip.BottomRight)
        {
            bottom = Math.Clamp(bottom + dy, frame.Y, frame.Bottom);
        }

        // FromPoints normalizes, so dragging an edge past its opposite flips the
        // rectangle instead of producing a negative size.
        return CaptureRect.FromPoints(left, top, right, bottom);
    }
}
