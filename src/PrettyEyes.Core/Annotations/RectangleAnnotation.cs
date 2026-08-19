using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

public sealed class RectangleAnnotation : IAnnotation
{
    private readonly uint _color;
    private readonly float _strokeWidth;

    public RectangleAnnotation(CaptureRect bounds, uint color, float strokeWidth)
    {
        Bounds = bounds;
        _color = color;
        _strokeWidth = strokeWidth;
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
    {
        using var paint = new SKPaint
        {
            Color = _color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            IsAntialias = true,
        };

        // Inset by half the stroke so the frame stays inside its own bounds.
        var inset = _strokeWidth / 2f;
        var rect = SKRect.Create(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
        rect.Inflate(-inset, -inset);

        canvas.DrawRect(rect, paint);
    }
}
