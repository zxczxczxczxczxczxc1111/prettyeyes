using SkiaSharp;

namespace PrettyEyes.Core.Laser;

/// <summary>
/// Draws a <see cref="LaserTrail"/>: a beam that is full width at the pointer
/// and narrows to nothing behind it.
///
/// A filled outline rather than a stroked polyline, which is how excalidraw
/// does it and the reason theirs looks like a comet instead of a rope. A
/// polyline has one width per segment, so a taper arrives as a staircase of
/// visible steps; an outline has a width per point and the edge between them is
/// a straight line nobody can see.
///
/// Three passes of that outline rather than one, which is where this stops
/// looking like a marker pen. A pen is one flat colour at one width; light has
/// a soft edge, a saturated body and a hot centre, and reads as light because
/// of the order those sit in. All three are plain fills of the same shape at
/// three widths - no blur, no second surface, nothing that costs a pass over
/// the screen.
///
/// The beam thins out rather than fading. Two shrinking numbers instead of one:
/// age, so a stroke left alone disappears, and distance back from the pointer,
/// so a stroke being drawn keeps a constant visible length however long the
/// gesture goes on.
///
/// One instance per surface, holding its one paint, its one path and one array
/// of widths, because this is called on every frame of a live screen.
/// </summary>
public sealed class LaserPainter : IDisposable
{
    /// <summary>
    /// 5.9:1 on black and 3.6:1 on white, so it is visible whatever is being
    /// demonstrated underneath.
    /// </summary>
    public static readonly SKColor Default = new(0xFF, 0x3B, 0x30);

    /// <summary>Half the width of the beam where the pointer is.</summary>
    private const float Reach = 4.5f;

    /// <summary>
    /// How far back along the stroke the taper runs, in pixels of the screen it
    /// is drawn on.
    ///
    /// Pixels rather than points, which is the whole difference between a beam
    /// that can underline a sentence and one that cannot. Counted in points,
    /// the visible length is however far the mouse happened to travel between
    /// reports: fifty points of a thousand-hertz mouse is a couple of
    /// centimetres, and fifty of a slow one is half the screen.
    ///
    /// Twelve hundred is a line of text across most of a screen, which is the
    /// thing people underline.
    /// </summary>
    private const float Span = 1200f;

    /// <summary>Below this the shape is thinner than a pixel and not worth a triangle.</summary>
    private const float Hairline = 0.2f;

    /// <summary>
    /// The soft edge. Wide and faint, and drawn first so everything else sits
    /// on top of it: this is the whole difference between a beam and a stripe
    /// of paint, and it costs one more fill of a path that is already built.
    /// </summary>
    private const float HaloWidth = 1.8f;
    private const byte HaloAlpha = 62;

    /// <summary>
    /// The hot centre, mixed most of the way to white. A real pointer is white
    /// in the middle and coloured at its edges, because the middle is brighter
    /// than the eye can take a colour from.
    /// </summary>
    private const float CoreWidth = 0.28f;
    private const float CoreWhiteness = 0.55f;

    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private readonly SKPath _path = new();

    /// <summary>
    /// Half the width at each point of the stroke being drawn, worked out once
    /// and read six times: three passes, two sides each.
    /// </summary>
    private float[] _width = new float[256];

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

        var core = Lighten(colour, CoreWhiteness);
        var from = 0;

        for (var index = 1; index <= points.Length; index++)
        {
            // A stroke runs up to the point that opens the next one.
            if (index < points.Length && !points[index].Break)
            {
                continue;
            }

            Measure(points, from, index);

            // Outside in. Each pass covers the middle of the one before it, so
            // what is left of the wider pass is its rim.
            Stroke(canvas, points, from, index, HaloWidth, colour.WithAlpha(HaloAlpha));
            Stroke(canvas, points, from, index, 1f, colour);
            Stroke(canvas, points, from, index, CoreWidth, core);

            from = index;
        }
    }

    public void Dispose()
    {
        _paint.Dispose();
        _path.Dispose();
    }

    /// <summary>
    /// White is not mixed in evenly: the centre of a beam is white and its
    /// shoulders keep the colour, so the channel that carries the colour gives
    /// way more slowly than the two that do not.
    /// </summary>
    private static SKColor Lighten(SKColor colour, float towards) => new(
        Mix(colour.Red, towards * 0.75f),
        Mix(colour.Green, towards),
        Mix(colour.Blue, towards),
        colour.Alpha);

    private static byte Mix(byte channel, float towards) =>
        (byte)Math.Clamp(channel + ((255 - channel) * towards), 0, 255);

    /// <summary>
    /// Half the width at every point of one stroke: the smaller of what its age
    /// allows and what its distance back from the pointer allows. A stroke has
    /// to disappear when it is old and taper when it is not.
    ///
    /// Walked from the pointer backwards, adding up the distance travelled,
    /// because that is the direction the answer depends on.
    /// </summary>
    private void Measure(ReadOnlySpan<LaserPoint> points, int from, int to)
    {
        var count = to - from;

        if (_width.Length < count)
        {
            _width = new float[Math.Max(count, _width.Length * 2)];
        }

        var back = 0f;

        for (var index = to - 1; index >= from; index--)
        {
            if (index < to - 1)
            {
                var dx = points[index + 1].X - points[index].X;
                var dy = points[index + 1].Y - points[index].Y;

                back += MathF.Sqrt((dx * dx) + (dy * dy));
            }

            var byAge = Ease(1f - points[index].Age);
            var byPlace = Ease(Math.Clamp(1f - (back / Span), 0f, 1f));

            _width[index - from] = Reach * Math.Min(byAge, byPlace);
        }
    }

    private void Stroke(
        SKCanvas canvas, ReadOnlySpan<LaserPoint> points, int from, int to, float scale, SKColor colour)
    {
        _paint.Color = colour;

        var count = to - from;
        var head = _width[count - 1] * scale;

        if (count == 1)
        {
            if (head >= Hairline)
            {
                canvas.DrawCircle(points[from].X, points[from].Y, head, _paint);
            }

            return;
        }

        _path.Reset();

        // Down one side and back up the other. The offsets are along the normal
        // to the direction of travel, taken from the neighbours rather than
        // from one segment, or every corner would pinch.
        Side(points, from, to, count, scale, forwards: true);
        Side(points, from, to, count, scale, forwards: false);

        _path.Close();
        canvas.DrawPath(_path, _paint);

        // A round head, which the two sides cannot give it: the pointer is the
        // part being looked at and it should not end in a chisel.
        if (head >= Hairline)
        {
            canvas.DrawCircle(points[to - 1].X, points[to - 1].Y, head, _paint);
        }
    }

    private void Side(
        ReadOnlySpan<LaserPoint> points, int from, int to, int count, float scale, bool forwards)
    {
        for (var step = 0; step < count; step++)
        {
            var index = forwards ? from + step : to - 1 - step;

            var (dx, dy) = Direction(points, from, to, index);
            var width = _width[index - from] * scale;

            // Perpendicular to the direction of travel, and the far side is the
            // same offset with the sign flipped.
            var nx = -dy * width;
            var ny = dx * width;

            var x = points[index].X + (forwards ? nx : -nx);
            var y = points[index].Y + (forwards ? ny : -ny);

            if (step == 0 && forwards)
            {
                _path.MoveTo(x, y);
            }
            else
            {
                _path.LineTo(x, y);
            }
        }
    }

    /// <summary>
    /// Which way the stroke is going at a point, taken from its neighbours so
    /// the outline turns a corner instead of folding over it.
    /// </summary>
    private static (float X, float Y) Direction(ReadOnlySpan<LaserPoint> points, int from, int to, int index)
    {
        var before = points[Math.Max(index - 1, from)];
        var after = points[Math.Min(index + 1, to - 1)];

        var dx = after.X - before.X;
        var dy = after.Y - before.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));

        // Two identical neighbours mean a stroke of one step; any direction
        // will do, and along the x axis is a direction.
        return length < 0.0001f ? (1f, 0f) : (dx / length, dy / length);
    }

    /// <summary>Quick off the mark and slow at the end, so most of the stroke is full width.</summary>
    private static float Ease(float k)
    {
        var left = 1f - k;

        return 1f - (left * left * left);
    }
}
