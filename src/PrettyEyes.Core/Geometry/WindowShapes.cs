namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Where the windows were when the screen was frozen, front to back.
///
/// Taken at capture time and carried with the frame, because by the time
/// anybody asks the question the answer would be us: the overlay covers every
/// monitor, so asking Windows what is under the pointer during a capture
/// returns the overlay itself.
///
/// The snapshot is a list of rectangles and nothing else. Handles, titles and
/// processes are the platform layer's business and stop at its edge; what the
/// overlay needs is which rectangle the pointer is in.
/// </summary>
public sealed class WindowShapes(IReadOnlyList<CaptureRect> frontToBack)
{
    /// <summary>Nothing was recorded. Every point answers null.</summary>
    public static WindowShapes None { get; } = new([]);

    public IReadOnlyList<CaptureRect> FrontToBack { get; } = frontToBack;

    /// <summary>
    /// The frontmost window covering this point, or null when the point is on
    /// bare desktop. Edges follow CaptureRect.Contains - left and top belong to
    /// the window, right and bottom to whatever is next door.
    /// </summary>
    public CaptureRect? At(int x, int y)
    {
        for (var i = 0; i < FrontToBack.Count; i++)
        {
            var shape = FrontToBack[i];

            // A window with no size covers nothing, and Contains would still
            // have to be asked about it on every click.
            if (shape.IsEmpty)
            {
                continue;
            }

            if (shape.Contains(x, y))
            {
                return shape;
            }
        }

        return null;
    }
}
