using SkiaSharp;

namespace PrettyEyes.Core.Laser;

/// <summary>
/// Draws a <see cref="LaserTrail"/>: a stripe that tapers and fades from the
/// pointer back down its tail, with a solid core where the pointer is.
///
/// One instance per surface, holding its one paint, because this is called on
/// every frame of a live screen and a paint per frame is a paint per frame.
///
/// No glow. A blurred halo is the first thing anyone reaches for here and the
/// last thing that survives being looked at on a bright screen: it costs a
/// second pass over the whole stripe and turns a red line into a pink cloud.
/// </summary>
public sealed class LaserPainter : IDisposable
{
    /// <summary>
    /// 5.9:1 on black and 3.6:1 on white, so it is visible whatever is being
    /// demonstrated underneath.
    /// </summary>
    public static readonly SKColor Default = new(0xFF, 0x3B, 0x30);

    private const float Head = 6f;
    private const float Tail = 1.5f;

    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    /// <summary>
    /// Takes a copy of the points rather than the trail. Drawing happens on the
    /// render thread and the trail is written on the UI one, so what arrives
    /// here is already frozen.
    /// </summary>
    public void Paint(SKCanvas canvas, ReadOnlySpan<LaserPoint> points, SKColor colour)
    {
        if (points.Length == 0)
        {
            return;
        }

        _paint.Style = SKPaintStyle.Stroke;

        var previous = points[0];

        for (var index = 1; index < points.Length; index++)
        {
            var point = points[index];

            // The younger end of the segment decides both, so the stripe
            // reaches full width and full colour by the time it arrives at the
            // pointer instead of one step short of it.
            _paint.StrokeWidth = Head - ((Head - Tail) * point.Age);
            _paint.Color = colour.WithAlpha(Fade(point.Age));

            canvas.DrawLine(previous.X, previous.Y, point.X, point.Y, _paint);

            previous = point;
        }

        var head = points[^1];

        // The core. Without it a pointer that has stopped for a moment is a
        // line ending in a point, and the eye looks for the end of a line but
        // lands on a dot.
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = colour;

        canvas.DrawCircle(head.X, head.Y, Head * 0.55f, _paint);
    }

    public void Dispose() => _paint.Dispose();

    /// <summary>
    /// Full strength for most of the life and then away quickly, rather than
    /// an even ramp: a stripe that starts dimming immediately never looks
    /// bright enough to point with.
    /// </summary>
    private static byte Fade(float age) => (byte)(255 * (1 - (age * age * age)));
}
