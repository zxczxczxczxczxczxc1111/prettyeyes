using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

public sealed class ArrowAnnotation : IAnnotation
{
    private readonly int _x1;
    private readonly int _y1;
    private readonly int _x2;
    private readonly int _y2;
    private readonly uint _color;
    private readonly float _strokeWidth;

    public ArrowAnnotation(int x1, int y1, int x2, int y2, uint color, float strokeWidth)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
        _color = color;
        _strokeWidth = strokeWidth;

        // Padded by the head length: the arrowhead sticks out past the line,
        // and Bounds has to cover everything the annotation actually paints.
        var line = CaptureRect.FromPoints(x1, y1, x2, y2);
        var pad = (int)Math.Ceiling(ArrowHead.Reach(strokeWidth, Length));
        Bounds = new CaptureRect(
            line.X - pad, line.Y - pad, line.Width + pad * 2, line.Height + pad * 2);
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin, BlurCache cache)
    {
        using var paint = new SKPaint
        {
            Color = _color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };

        var angle = Math.Atan2(_y2 - _y1, _x2 - _x1);
        var head = ArrowHead.Length(_strokeWidth, Length);
        var stop = head * ArrowHead.BaseAlong;

        // Stopped short of the tip rather than run into it. The stroke has a
        // round cap, so drawn all the way it would bulge half a width past the
        // point of the triangle and blunt it; ending under the fill hides both
        // the cap and the joint.
        canvas.DrawLine(
            _x1,
            _y1,
            _x2 - (float)(Math.Cos(angle) * stop),
            _y2 - (float)(Math.Sin(angle) * stop),
            paint);

        ArrowHead.Draw(canvas, _color, _x2, _y2, angle, head);
    }

    /// <summary>How long the arrow is, tip to tail.</summary>
    private double Length => Math.Sqrt(
        ((double)(_x2 - _x1) * (_x2 - _x1)) + ((double)(_y2 - _y1) * (_y2 - _y1)));
}
