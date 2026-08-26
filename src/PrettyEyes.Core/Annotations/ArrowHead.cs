using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// Two strokes back from a tip. Shared by both arrows so they read as one tool
/// in two moods rather than as two tools that happen to have arrowheads.
/// </summary>
public static class ArrowHead
{
    private const float LengthFactor = 4f;
    private const double Spread = Math.PI / 7;

    /// <summary>How far back from the tip the branches reach.</summary>
    public static float Length(float strokeWidth) => strokeWidth * LengthFactor;

    /// <param name="angle">
    /// Where the line arrives from, as Atan2 of the incoming direction. The
    /// branches are drawn back along it, which is why Pi is added below.
    /// </param>
    public static void Draw(
        SKCanvas canvas, SKPaint paint, float tipX, float tipY, double angle, float strokeWidth)
    {
        var length = Length(strokeWidth);

        for (var side = -1; side <= 1; side += 2)
        {
            var branch = angle + Math.PI + (side * Spread);
            var x = tipX + (float)(Math.Cos(branch) * length);
            var y = tipY + (float)(Math.Sin(branch) * length);

            canvas.DrawLine(tipX, tipY, x, y, paint);
        }
    }
}
