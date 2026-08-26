using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// A line drawn by hand with an arrowhead at the end of it, the way a marker on
/// a photograph works. Separate from ArrowAnnotation because that one is two
/// points and this one is a whole gesture; separate from StrokeAnnotation
/// because a highlighter has no business growing a head.
/// </summary>
public sealed class FreehandArrowAnnotation : IAnnotation
{
    private readonly int[] _x;
    private readonly int[] _y;
    private readonly uint _color;
    private readonly float _strokeWidth;

    public FreehandArrowAnnotation(
        IReadOnlyList<(int X, int Y)> points, uint color, float strokeWidth)
    {
        _x = new int[points.Count];
        _y = new int[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            _x[i] = points[i].X;
            _y[i] = points[i].Y;
        }

        _color = color;
        _strokeWidth = strokeWidth;

        // Padded by the head: the branches stick out past the last point, and
        // Bounds has to cover everything the annotation actually paints.
        var pad = (int)Math.Ceiling(ArrowHead.Length(strokeWidth)) + 1;
        var left = _x.Min();
        var top = _y.Min();

        Bounds = new CaptureRect(
            left - pad,
            top - pad,
            _x.Max() - left + (pad * 2),
            _y.Max() - top + (pad * 2));
    }

    public CaptureRect Bounds { get; }

    /// <summary>
    /// How far back the direction is measured.
    ///
    /// Fixed, and deliberately not tied to the stroke width: a hand shakes by
    /// the same couple of pixels whether the line is a hairline or a band, and
    /// the old rule of four times the width gave a thin arrow four pixels of
    /// shaky input to decide on - up to 45 degrees off at the ninety-fifth
    /// percentile. Twenty came out of measuring three ways of drawing:
    /// straight, curving into the target, and slowing down to aim. Past
    /// twenty-four a straight line keeps getting steadier while a curve starts
    /// lagging, which points the head away from what the hand was aiming at.
    /// </summary>
    public const int TailLength = 20;

    /// <summary>
    /// How far the tip has to be from the far end of the tail before a head is
    /// drawn at all. Below this the gesture is a dot, and a dot with an
    /// arrowhead on it is a blot. Absolute rather than a multiple of the width,
    /// because a fat arrow a hundred pixels long is still an arrow.
    /// </summary>
    public const int MinHeadStroke = 8;

    /// <summary>
    /// Which way the head faces, or null when the gesture is too short to have
    /// a direction at all.
    ///
    /// Not the angle of the last segment: the last samples of a slow stroke are
    /// the hand rather than the intent, and at two pixels apart they decide
    /// nothing worth following. Averaging the tail was measured and dropped -
    /// against a slow tremor, which is how a hand actually shakes, it is no
    /// better than this and worse over a short tail.
    /// </summary>
    public static double? HeadAngle(IReadOnlyList<int> x, IReadOnlyList<int> y)
    {
        if (x.Count < 2 || x.Count != y.Count)
        {
            return null;
        }

        var tipX = x[^1];
        var tipY = y[^1];
        var from = 0;

        // Walk back until the tail is long enough, or until the stroke runs
        // out - a short arrow uses all of itself rather than going without.
        for (var i = x.Count - 2; i >= 0; i--)
        {
            from = i;

            if (Distance(tipX - x[i], tipY - y[i]) >= TailLength)
            {
                break;
            }
        }

        var dx = tipX - x[from];
        var dy = tipY - y[from];

        return Distance(dx, dy) < MinHeadStroke ? null : Math.Atan2(dy, dx);
    }

    private static double Distance(int dx, int dy) => Math.Sqrt((dx * dx) + (dy * dy));

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin, BlurCache cache)
    {
        using var path = StrokePath.Build(_x, _y);

        using var paint = new SKPaint
        {
            Color = new SKColor(_color),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };

        canvas.DrawPath(path, paint);

        if (HeadAngle(_x, _y) is { } angle)
        {
            ArrowHead.Draw(canvas, paint, _x[^1], _y[^1], angle, _strokeWidth);
        }
    }
}
