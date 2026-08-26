using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// The filled triangle at the end of an arrow. Shared by both arrows so they
/// read as one tool in two moods rather than as two tools that happen to have
/// arrowheads.
///
/// Filled rather than two strokes, and that is the whole reason it looks clean.
/// Two separate strokes let the eye compare their angles to each other, so ten
/// degrees of error reads as a bent tick; a solid triangle has nothing to
/// compare and stays an arrow. Borrowed from the editor in Telegram, and the
/// same shape the product's own site has been drawing all along.
/// </summary>
public static class ArrowHead
{
    /// <summary>
    /// How long the head is as a multiple of the line it ends, and how far its
    /// base corners swing out from the axis.
    ///
    /// Both measured off the editor in Telegram rather than guessed, on five
    /// arrows whose numbers agreed: against a 23 pixel stroke their head runs
    /// 2.6 strokes deep and 3.0 wide. The first attempt here was four deep and
    /// a 25 degree spread, which is a long thin spike - reported as "too
    /// sharp", and the measurement says why.
    /// </summary>
    private const float LengthFactor = 3f;
    private const double Spread = Math.PI / 6;

    /// <summary>
    /// The head's outline, stroked in its own colour with round joints, as a
    /// fraction of the head's own length.
    ///
    /// This is what blunts the point and softens the base corners. Measured:
    /// their arrowhead is a filled shape - 83% of its convex hull - yet at the
    /// very tip it is still about one stroke wide instead of coming to nothing.
    /// A bare triangle looks like a blade next to it.
    ///
    /// Taken off the head rather than off the line on purpose. At the width the
    /// measurement was made the two are the same number, but the head has a
    /// floor and the line does not, so a hairline would otherwise get a head of
    /// full length with a rounding of half a pixel - sharper than every other
    /// arrow in the same picture.
    /// </summary>
    private const float OutlineFactor = 0.15f;

    /// <summary>
    /// The shortest a head is allowed to be, whatever the line.
    ///
    /// Without it the head is purely a multiple of the width, so a hairline
    /// gets a head three pixels long: reported as "at small sizes it is
    /// practically invisible", and it was, because the head drowned in the
    /// line it was supposed to end.
    /// </summary>
    public const float MinLength = 12f;

    /// <summary>
    /// Where the line should stop, as a fraction of the head length back from
    /// the tip. Short of the base corners, so the line ends underneath the fill
    /// rather than meeting it edge to edge.
    /// </summary>
    public const float BaseAlong = 0.7f;

    /// <summary>
    /// How far back from the tip the head reaches, capped by the stroke it sits
    /// on.
    ///
    /// The cap is not decoration. Four times the width has no ceiling and the
    /// width now goes to sixty, so a ten-pixel flick would grow a head of two
    /// hundred and forty - the blot this tool refuses to draw, only enormous.
    /// Half the stroke also keeps the trimming safe: whatever happens, two
    /// thirds of the line survives under the head.
    /// </summary>
    public static float Length(float strokeWidth, double strokeLength) =>
        (float)Math.Min(Math.Max(strokeWidth * LengthFactor, MinLength), strokeLength / 2);

    /// <summary>
    /// How far the head reaches from the tip once its outline is counted. What
    /// Bounds has to cover; Length alone leaves the outline outside the box.
    /// </summary>
    public static float Reach(float strokeWidth, double strokeLength)
    {
        var length = Length(strokeWidth, strokeLength);

        return length + (length * OutlineFactor / 2);
    }

    /// <param name="angle">
    /// Where the line arrives from, as Atan2 of the incoming direction. The
    /// base corners are laid back along it, which is why Pi is added below.
    /// </param>
    public static void Draw(
        SKCanvas canvas, uint color, float tipX, float tipY, double angle, float headLength)
    {
        using var path = new SKPath();

        path.MoveTo(tipX, tipY);

        for (var side = -1; side <= 1; side += 2)
        {
            var branch = angle + Math.PI + (side * Spread);

            path.LineTo(
                tipX + (float)(Math.Cos(branch) * headLength),
                tipY + (float)(Math.Sin(branch) * headLength));
        }

        path.Close();

        // Filled and outlined in one pass, in the same colour. The outline is
        // what rounds the point and the base corners; a bare fill comes to a
        // needle, which is what "too sharp" meant.
        using var paint = new SKPaint
        {
            Color = new SKColor(color),
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = headLength * OutlineFactor,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };

        canvas.DrawPath(path, paint);
    }
}
