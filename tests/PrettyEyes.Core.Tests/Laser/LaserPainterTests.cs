using PrettyEyes.Core.Laser;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Laser;

/// <summary>
/// What the trail looks like once it is pixels.
///
/// The shape is the whole point of a laser pointer: a stripe of even width
/// reads as a scribble, and a stripe that fades from the wrong end reads as
/// something being drawn backwards.
/// </summary>
public class LaserPainterTests
{
    private const int Width = 200;
    private const int Height = 100;

    [Fact]
    public void An_empty_trail_leaves_the_surface_alone()
    {
        using var sheet = Paint(new LaserTrail(TimeSpan.FromMilliseconds(900)));

        Assert.Equal(0, Lit(sheet));
    }

    [Fact]
    public void The_pointer_end_is_the_colour_it_was_given()
    {
        using var sheet = Paint(Streak());

        var head = sheet.GetPixel(180, 50);

        Assert.Equal(LaserPainter.Default.Red, head.Red);
        Assert.Equal(LaserPainter.Default.Green, head.Green);
        Assert.Equal(LaserPainter.Default.Blue, head.Blue);
    }

    /// <summary>
    /// Thick at the pointer, thin at the far end. Backwards, the trail looks
    /// like it is being sucked in rather than left behind.
    /// </summary>
    [Fact]
    public void The_stripe_is_thicker_at_the_pointer_than_at_its_far_end()
    {
        using var sheet = Paint(Streak());

        Assert.True(
            Column(sheet, 178) > Column(sheet, 22) + 1,
            $"head {Column(sheet, 178)} points, tail {Column(sheet, 22)}");
    }

    [Fact]
    public void The_far_end_is_fainter_than_the_pointer()
    {
        using var sheet = Paint(Streak());

        Assert.True(
            sheet.GetPixel(22, 50).Red < sheet.GetPixel(178, 50).Red,
            "the far end is not fading");
    }

    /// <summary>
    /// One reported position is a pointer that has just appeared, not an error.
    /// </summary>
    [Fact]
    public void A_single_point_still_shows_a_pointer()
    {
        var trail = new LaserTrail(TimeSpan.FromMilliseconds(900));
        trail.Add(100, 50, TimeSpan.Zero);

        using var sheet = Paint(trail);

        Assert.True(sheet.GetPixel(100, 50).Red > 200, "nothing was drawn");
    }

    [Fact]
    public void Nothing_is_painted_away_from_where_the_pointer_went()
    {
        using var sheet = Paint(Streak());

        Assert.Equal(0u, sheet.GetPixel(5, 5).Red);
        Assert.Equal(0u, sheet.GetPixel(195, 95).Red);
    }

    /// <summary>
    /// Nine positions across the sheet, sixty milliseconds apart, read at the
    /// moment the last one arrived. The far end is then a bit over half way
    /// through its life: still drawn, visibly older.
    /// </summary>
    private static LaserTrail Streak()
    {
        var trail = new LaserTrail(TimeSpan.FromMilliseconds(900));

        for (var step = 0; step < 9; step++)
        {
            trail.Add(20 + (step * 20), 50, TimeSpan.FromMilliseconds(step * 60));
        }

        return trail;
    }

    private static SKBitmap Paint(LaserTrail trail)
    {
        var sheet = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(SKColors.Black);

        Span<LaserPoint> points = stackalloc LaserPoint[trail.Count];
        trail.CopyTo(points);

        using var painter = new LaserPainter();
        painter.Paint(canvas, points, LaserPainter.Default);

        return sheet;
    }

    private static int Column(SKBitmap sheet, int x) =>
        Enumerable.Range(0, Height).Count(y => sheet.GetPixel(x, y).Red > 20);

    private static int Lit(SKBitmap sheet) =>
        Enumerable.Range(0, Width).Sum(x => Column(sheet, x));
}
