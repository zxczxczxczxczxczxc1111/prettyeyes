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
    /// Which way the head faces, or null when the gesture is too short to have
    /// a direction at all.
    ///
    /// Deliberately not the angle of the last segment. The last two points are
    /// a couple of pixels apart - that is the hand, not the intent - and a head
    /// built on them spins on the spot. The direction is measured from the last
    /// point that sits a whole head-length away from the tip, which is the same
    /// distance the head itself occupies.
    /// </summary>
    public static double? HeadAngle(IReadOnlyList<int> x, IReadOnlyList<int> y, float strokeWidth)
    {
        if (x.Count < 2 || x.Count != y.Count)
        {
            return null;
        }

        var reach = ArrowHead.Length(strokeWidth);
        var tipX = x[^1];
        var tipY = y[^1];

        for (var i = x.Count - 2; i >= 0; i--)
        {
            var dx = tipX - x[i];
            var dy = tipY - y[i];

            if ((dx * dx) + (dy * dy) >= reach * reach)
            {
                return Math.Atan2(dy, dx);
            }
        }

        return null;
    }

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

        if (HeadAngle(_x, _y, _strokeWidth) is { } angle)
        {
            ArrowHead.Draw(canvas, paint, _x[^1], _y[^1], angle, _strokeWidth);
        }
    }
}
