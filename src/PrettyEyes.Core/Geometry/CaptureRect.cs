namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Rectangle in physical pixels of the virtual desktop. Coordinates can be
/// negative: a monitor placed left of or above the primary one starts there.
///
/// Deliberately not named PixelRect - Avalonia has its own type with that name
/// in its root namespace, and the clash would surface in every UI file.
///
/// Contains() is half-open on the right and bottom edges so that adjacent
/// monitors never both claim the same pixel.
/// </summary>
public readonly record struct CaptureRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static CaptureRect Empty => new(0, 0, 0, 0);

    public static CaptureRect FromPoints(int x1, int y1, int x2, int y2)
    {
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Max(y1, y2);

        return new CaptureRect(left, top, right - left, bottom - top);
    }

    public CaptureRect Intersect(CaptureRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top
            ? Empty
            : new CaptureRect(left, top, right - left, bottom - top);
    }

    public bool Contains(int x, int y) =>
        x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>
    /// Nearest point that still belongs to this rectangle. Right and bottom are
    /// exclusive, same as Contains, so the last valid pixel is one less.
    /// </summary>
    public (int X, int Y) ClampPoint(int x, int y) =>
        (Math.Clamp(x, X, Math.Max(X, Right - 1)), Math.Clamp(y, Y, Math.Max(Y, Bottom - 1)));
}
