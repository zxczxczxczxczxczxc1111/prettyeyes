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
    private const float LengthFactor = 4f;
    private const double Spread = Math.PI / 7;

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
        (float)Math.Min(strokeWidth * LengthFactor, strokeLength / 2);

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

        using var paint = new SKPaint
        {
            Color = new SKColor(color),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        canvas.DrawPath(path, paint);
    }
}
