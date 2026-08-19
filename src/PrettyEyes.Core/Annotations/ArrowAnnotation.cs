using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

public sealed class ArrowAnnotation : IAnnotation
{
    private const float HeadLengthFactor = 4f;
    private const double HeadAngle = Math.PI / 7;

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
        var pad = (int)Math.Ceiling(strokeWidth * HeadLengthFactor);
        Bounds = new CaptureRect(
            line.X - pad, line.Y - pad, line.Width + pad * 2, line.Height + pad * 2);
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
    {
        using var paint = new SKPaint
        {
            Color = _color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };

        canvas.DrawLine(_x1, _y1, _x2, _y2, paint);

        var angle = Math.Atan2(_y2 - _y1, _x2 - _x1);
        var headLength = _strokeWidth * HeadLengthFactor;

        for (var side = -1; side <= 1; side += 2)
        {
            var branch = angle + Math.PI + side * HeadAngle;
            var x = _x2 + (float)(Math.Cos(branch) * headLength);
            var y = _y2 + (float)(Math.Sin(branch) * headLength);
            canvas.DrawLine(_x2, _y2, x, y, paint);
        }
    }
}
