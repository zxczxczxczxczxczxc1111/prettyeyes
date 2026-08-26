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

    /// <summary>
    /// Whether this one is finished enough to wear a head.
    ///
    /// Named for permission rather than for fact: a head is drawn when this is
    /// true <b>and</b> the gesture turned out to have a direction, so a
    /// three-pixel stroke has the flag up and no head at all.
    /// </summary>
    public bool HeadAllowed { get; }

    public FreehandArrowAnnotation(
        IReadOnlyList<(int X, int Y)> points, uint color, float strokeWidth, bool headAllowed = true)
    {
        HeadAllowed = headAllowed;

        _x = new int[points.Count];
        _y = new int[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            _x[i] = points[i].X;
            _y[i] = points[i].Y;
        }

        _color = color;
        _strokeWidth = strokeWidth;

        // Padded by the head: it sticks out past the last point, and Bounds has
        // to cover everything the annotation actually paints. Deliberately not
        // conditional on HeadAllowed - a preview and the finished arrow have to
        // claim the same ground, or the bounds jump on release and a
        // neighbouring monitor keeps a strip nobody repainted.
        var pad = (int)Math.Ceiling(ArrowHead.Reach(strokeWidth, Span)) + 1;

        // One pass rather than four. The constructor runs on every pointer
        // move: the tools build a fresh annotation for the preview each time,
        // and four walks of the same two arrays is three too many.
        var left = _x[0];
        var right = _x[0];
        var top = _y[0];
        var bottom = _y[0];

        for (var i = 1; i < _x.Length; i++)
        {
            if (_x[i] < left) { left = _x[i]; }
            if (_x[i] > right) { right = _x[i]; }
            if (_y[i] < top) { top = _y[i]; }
            if (_y[i] > bottom) { bottom = _y[i]; }
        }

        Bounds = new CaptureRect(
            left - pad,
            top - pad,
            right - left + (pad * 2),
            bottom - top + (pad * 2));
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
        using var paint = new SKPaint
        {
            Color = new SKColor(_color),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };

        if (!HeadAllowed || HeadAngle(_x, _y) is not { } angle)
        {
            using var whole = StrokePath.Build(_x, _y);

            canvas.DrawPath(whole, paint);

            return;
        }

        var head = ArrowHead.Length(_strokeWidth, Span);

        using var trimmed = TrimmedPath(angle, head * ArrowHead.BaseAlong);

        canvas.DrawPath(trimmed, paint);
        ArrowHead.Draw(canvas, _color, _x[^1], _y[^1], angle, head);
    }

    /// <summary>How far the gesture got, first point to last, as the crow flies.</summary>
    private double Span => Distance(_x[^1] - _x[0], _y[^1] - _y[0]);

    /// <summary>
    /// The line with its last stretch replaced by one straight run into the
    /// head.
    ///
    /// Without this the fix would be half a fix. The smoothing ends with a
    /// straight segment into the raw last point, which is the very sample the
    /// direction stopped trusting: the head would sit true and the line under
    /// it would still wander off by the same twenty degrees, welded to the
    /// triangle at an angle. Cut back to the head's base and aimed along it,
    /// line and head are collinear by construction rather than by luck.
    /// </summary>
    private SKPath TrimmedPath(double angle, float stop)
    {
        var keep = _x.Length;

        while (keep > 1 && Distance(_x[^1] - _x[keep - 1], _y[^1] - _y[keep - 1]) < stop)
        {
            keep--;
        }

        var x = new int[keep + 1];
        var y = new int[keep + 1];

        Array.Copy(_x, x, keep);
        Array.Copy(_y, y, keep);

        x[keep] = (int)Math.Round(_x[^1] - (Math.Cos(angle) * stop));
        y[keep] = (int)Math.Round(_y[^1] - (Math.Sin(angle) * stop));

        return StrokePath.Build(x, y);
    }
}
