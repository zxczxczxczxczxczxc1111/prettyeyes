using PrettyEyes.Core.Laser;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Laser;

/// <summary>
/// What the trail looks like once it is pixels.
///
/// The shape is the whole point of a laser pointer: a stripe of even width
/// reads as a scribble, and a stripe that tapers from the wrong end reads as
/// something being drawn backwards. Nothing here assumes an exact coordinate -
/// the points are smoothed on the way in, so the drawing is checked by looking
/// at what got painted rather than by probing where it was supposed to land.
/// </summary>
public class LaserPainterTests
{
    private const int Width = 400;
    private const int Height = 200;

    [Fact]
    public void An_empty_trail_leaves_the_surface_alone()
    {
        using var sheet = Paint(new LaserTrail(TimeSpan.FromMilliseconds(900)));

        Assert.Equal(0, Lit(sheet));
    }

    [Fact]
    public void A_stroke_gets_painted()
    {
        using var sheet = Paint(Streak());

        Assert.True(Lit(sheet) > 200, $"only {Lit(sheet)} points were painted");
    }

    /// <summary>
    /// Thick at the pointer, thin at the far end. Backwards, the trail looks
    /// like it is being sucked in rather than left behind.
    /// </summary>
    [Fact]
    public void The_stripe_is_thicker_at_the_pointer_than_at_its_far_end()
    {
        using var sheet = Paint(Streak());

        var columns = Enumerable.Range(0, Width).Where(x => Column(sheet, x) > 0).ToList();
        var tail = Column(sheet, columns[0] + 2);
        var head = Column(sheet, columns[^1] - 2);

        Assert.True(head > tail, $"head {head} points, tail {tail}");
    }

    [Fact]
    public void The_stroke_is_the_colour_it_was_given()
    {
        using var sheet = Paint(Streak());

        var brightest = All(sheet).Max(p => sheet.GetPixel(p.x, p.y).Red);

        Assert.Equal(LaserPainter.Default.Red, brightest);
    }

    /// <summary>
    /// One reported position is a pointer that has just been put down, not an
    /// error.
    /// </summary>
    [Fact]
    public void A_single_point_still_shows_a_pointer()
    {
        var trail = new LaserTrail(TimeSpan.FromMilliseconds(900));
        trail.Begin();
        trail.Add(200, 100, TimeSpan.Zero);

        using var sheet = Paint(trail);

        Assert.True(sheet.GetPixel(200, 100).Red > 200, "nothing was drawn");
    }

    [Fact]
    public void Nothing_is_painted_away_from_where_the_pointer_went()
    {
        using var sheet = Paint(Streak());

        Assert.Equal(0u, sheet.GetPixel(5, 5).Red);
        Assert.Equal(0u, sheet.GetPixel(395, 195).Red);
    }

    /// <summary>
    /// Two presses in two places are two strokes. Joined, the drawing runs a
    /// line between them that the pointer never travelled.
    /// </summary>
    [Fact]
    public void Two_strokes_are_not_joined_across_the_gap_between_them()
    {
        var trail = new LaserTrail(TimeSpan.FromMilliseconds(900));

        trail.Begin();

        for (var step = 0; step < 6; step++)
        {
            trail.Add(20 + (step * 8), 40, TimeSpan.FromMilliseconds(step * 16));
        }

        trail.Begin();

        for (var step = 0; step < 6; step++)
        {
            trail.Add(300 + (step * 8), 160, TimeSpan.FromMilliseconds(100 + (step * 16)));
        }

        using var sheet = Paint(trail);

        Assert.True(sheet.GetPixel(30, 40).Red > 100, "the first stroke is missing");
        Assert.True(sheet.GetPixel(310, 160).Red > 100, "the second stroke is missing");
        Assert.Equal(0u, sheet.GetPixel(170, 100).Red);
    }

    /// <summary>
    /// A full stroke: a second of reports at sixty a second, which is as long
    /// as a trail ever gets and the only length at which the taper is fully
    /// stretched out.
    /// </summary>
    private static LaserTrail Streak()
    {
        var trail = new LaserTrail(TimeSpan.FromMilliseconds(900));

        trail.Begin();

        for (var step = 0; step < 54; step++)
        {
            trail.Add(40 + (step * 6), 100, TimeSpan.FromMilliseconds(step * 16));
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

    private static IEnumerable<(int x, int y)> All(SKBitmap sheet) =>
        Enumerable.Range(0, Height).SelectMany(y => Enumerable.Range(0, Width).Select(x => (x, y)));
}
