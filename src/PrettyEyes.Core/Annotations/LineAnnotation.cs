using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// A plain segment: the arrow without its head, for underlining and crossing
/// things out rather than pointing at them.
/// </summary>
public sealed class LineAnnotation : IAnnotation
{
    private readonly int _x1;
    private readonly int _y1;
    private readonly int _x2;
    private readonly int _y2;
    private readonly uint _color;
    private readonly float _strokeWidth;

    public LineAnnotation(int x1, int y1, int x2, int y2, uint color, float strokeWidth)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
        _color = color;
        _strokeWidth = strokeWidth;

        // Padded by half the stroke: a round cap reaches past the end point.
        var line = CaptureRect.FromPoints(x1, y1, x2, y2);
        var pad = (int)Math.Ceiling(strokeWidth / 2f) + 1;
        Bounds = new CaptureRect(
            line.X - pad, line.Y - pad, line.Width + (pad * 2), line.Height + (pad * 2));
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
    }
}
