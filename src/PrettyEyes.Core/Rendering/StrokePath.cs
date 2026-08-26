using SkiaSharp;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Points to a curve. Each segment runs to the midpoint of the next one with
/// the point itself as the control, which is the cheapest smoothing there is
/// and turns the polyline the mouse reports into something that looks drawn
/// rather than plotted.
///
/// Shared rather than copied: the pencil and the freehand arrow draw the same
/// line and differ only in what sits at its end. Two copies would be two places
/// to fix the day the smoothing changes.
/// </summary>
public static class StrokePath
{
    public static SKPath Build(IReadOnlyList<int> x, IReadOnlyList<int> y)
    {
        var path = new SKPath();

        if (x.Count == 1)
        {
            // A tap still leaves a dot: a zero-length path strokes nothing.
            path.MoveTo(x[0], y[0]);
            path.LineTo(x[0] + 0.01f, y[0]);

            return path;
        }

        path.MoveTo(x[0], y[0]);

        for (var i = 1; i < x.Count - 1; i++)
        {
            var midX = (x[i] + x[i + 1]) / 2f;
            var midY = (y[i] + y[i + 1]) / 2f;

            path.QuadTo(x[i], y[i], midX, midY);
        }

        path.LineTo(x[^1], y[^1]);

        return path;
    }
}
