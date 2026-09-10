using PrettyEyes.Core.Laser;
using Xunit;

namespace PrettyEyes.Core.Tests.Laser;

/// <summary>
/// The tail behind the pointer: what is in it, in what order, and when a point
/// stops counting.
///
/// Modelled on the way excalidraw does it, which is the thing this was asked to
/// feel like: a stroke starts when the button goes down and ends when it comes
/// up, reported positions are pulled towards the previous one before being
/// kept, and a position identical to the last one is not a position.
/// </summary>
public class LaserTrailTests
{
    private static readonly TimeSpan Life = TimeSpan.FromMilliseconds(900);

    [Fact]
    public void A_fresh_trail_has_nothing_in_it()
    {
        Assert.Equal(0, new LaserTrail(Life).Count);
    }

    /// <summary>
    /// The first point of a stroke is where the button went down, exactly.
    /// Smoothed against nothing it would be smoothed towards wherever the
    /// previous stroke happened to end.
    /// </summary>
    [Fact]
    public void The_first_point_of_a_stroke_is_taken_as_reported()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(10, 20, TimeSpan.Zero);

        Assert.Equal(1, trail.Count);
        Assert.Equal(10, trail[0].X);
        Assert.Equal(20, trail[0].Y);
    }

    /// <summary>
    /// A mouse reports a jagged line and the arm holding it did not draw one.
    /// Each new point is pulled part of the way towards the reported one and no
    /// further, which is what turns a staircase into a curve.
    /// </summary>
    [Fact]
    public void Every_point_after_the_first_is_pulled_towards_the_one_before()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(0, 0, TimeSpan.Zero);
        trail.Add(100, 0, TimeSpan.FromMilliseconds(16));

        Assert.True(trail[1].X > 0 && trail[1].X < 100, $"kept {trail[1].X} unchanged");
    }

    /// <summary>
    /// Smoothing is a lag, not a leash. A pointer moving steadily has to be
    /// followed at a fixed distance behind, and that distance measured in
    /// pixels rather than left to be whatever it turns out to be.
    /// </summary>
    [Fact]
    public void Smoothing_follows_the_pointer_at_a_fixed_distance_behind_it()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();

        for (var tick = 0; tick < 40; tick++)
        {
            trail.Add(tick * 10, 0, TimeSpan.FromMilliseconds(tick));
        }

        var head = trail[trail.Count - 1].X;

        Assert.InRange(head, 380, 390);
    }

    /// <summary>
    /// Oldest first, so drawing is a walk from the far end of the tail up to
    /// the pointer. Reversed, the taper would point the wrong way and the head
    /// would be the thin end.
    /// </summary>
    [Fact]
    public void Points_come_back_oldest_first()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();

        for (var step = 0; step < 5; step++)
        {
            trail.Add(step * 40, 0, TimeSpan.FromMilliseconds(step * 16));
        }

        for (var index = 1; index < trail.Count; index++)
        {
            Assert.True(trail[index].X > trail[index - 1].X, $"point {index} went backwards");
        }
    }

    [Fact]
    public void Age_runs_from_the_tail_down_to_nothing_at_the_head()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();

        for (var tick = 0; tick < 10; tick++)
        {
            trail.Add(tick * 10, 0, TimeSpan.FromMilliseconds(tick * 16));
        }

        trail.Advance(TimeSpan.FromMilliseconds(9 * 16));

        for (var index = 1; index < trail.Count; index++)
        {
            Assert.True(
                trail[index].Age < trail[index - 1].Age,
                $"point {index} is not younger than {index - 1}");
        }

        Assert.Equal(0, trail[trail.Count - 1].Age, 3);
    }

    [Fact]
    public void Half_a_lifetime_in_is_half_aged()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(0, 0, TimeSpan.Zero);
        trail.Advance(Life / 2);

        Assert.Equal(0.5, trail[0].Age, 2);
    }

    [Fact]
    public void A_point_past_its_life_is_gone()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(0, 0, TimeSpan.Zero);
        trail.Advance(Life + TimeSpan.FromMilliseconds(1));

        Assert.Equal(0, trail.Count);
    }

    /// <summary>
    /// The tail expires from its far end, which is the only order that keeps
    /// the remaining points contiguous.
    /// </summary>
    [Fact]
    public void Expiry_takes_the_oldest_and_leaves_the_rest()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(500, 500, Life / 2);
        trail.Advance(Life + TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, trail.Count);
        Assert.True(trail[0].X > 1, "the wrong point survived");
    }

    [Fact]
    public void The_buffer_never_grows_past_what_it_was_given()
    {
        var trail = new LaserTrail(Life, capacity: 8);

        trail.Begin();

        for (var tick = 0; tick < 40; tick++)
        {
            trail.Add(tick * 10, 0, TimeSpan.FromMilliseconds(tick));
        }

        Assert.Equal(8, trail.Count);
    }

    /// <summary>
    /// A full buffer drops the far end of the tail, never the pointer. The
    /// other way round the trail would trail ahead of the cursor.
    /// </summary>
    [Fact]
    public void A_full_buffer_gives_up_the_tail_and_keeps_the_head()
    {
        var trail = new LaserTrail(Life, capacity: 8);

        trail.Begin();

        for (var tick = 0; tick < 40; tick++)
        {
            trail.Add(tick * 10, 0, TimeSpan.FromMilliseconds(tick));
        }

        Assert.True(trail[7].X > trail[0].X, "the head is not the newest point");
        Assert.True(trail[7].X > 300, $"the head lagged behind at {trail[7].X}");
    }

    /// <summary>
    /// A pointer that has stopped reports the same place sixty times a second.
    /// Recorded, that fills the buffer with one point and the trail stops
    /// fading, which is the opposite of what standing still should look like.
    /// </summary>
    [Fact]
    public void Standing_still_does_not_refill_the_trail()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();

        for (var tick = 0; tick < 30; tick++)
        {
            trail.Add(400, 300, TimeSpan.FromMilliseconds(tick * 16));
        }

        Assert.Equal(1, trail.Count);
    }

    [Fact]
    public void Standing_still_lets_the_trail_run_out()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();

        for (var tick = 0; tick < 120; tick++)
        {
            trail.Add(400, 300, TimeSpan.FromMilliseconds(tick * 16));
        }

        Assert.Equal(0, trail.Count);
    }

    /// <summary>
    /// A stroke that has ended keeps fading where it lies. Cleared instead, a
    /// second press would take the previous one off the screen mid-fade, which
    /// reads as a glitch rather than as a new stroke.
    /// </summary>
    [Fact]
    public void A_new_stroke_leaves_the_old_one_fading()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(100, 100, TimeSpan.Zero);
        trail.Add(200, 100, TimeSpan.FromMilliseconds(16));

        trail.Begin();
        trail.Add(900, 900, TimeSpan.FromMilliseconds(200));

        Assert.Equal(3, trail.Count);
    }

    /// <summary>
    /// Without the break the drawing joins the end of one stroke to the start
    /// of the next, painting a line across the screen nobody drew.
    /// </summary>
    [Fact]
    public void A_new_stroke_is_marked_so_the_drawing_does_not_join_them()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(100, 100, TimeSpan.Zero);
        trail.Add(200, 100, TimeSpan.FromMilliseconds(16));

        trail.Begin();
        trail.Add(900, 900, TimeSpan.FromMilliseconds(200));

        Assert.True(trail[0].Break, "the first point of the trail starts a stroke");
        Assert.False(trail[1].Break);
        Assert.True(trail[2].Break, "the second stroke was not marked");
    }

    /// <summary>
    /// A pointer flicked across a 4K screen moves further in one frame than
    /// most gestures do in total. The streak is drawn as it happened: breaking
    /// the trail on distance would also break every fast honest movement.
    /// </summary>
    [Fact]
    public void A_pointer_thrown_across_the_screen_leaves_one_trail_not_two()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(0, 0, TimeSpan.Zero);
        trail.Add(3800, 2000, TimeSpan.FromMilliseconds(16));

        Assert.Equal(2, trail.Count);
        Assert.False(trail[1].Break);
    }

    /// <summary>
    /// Drawing happens on the render thread while this trail keeps being
    /// written on the UI one. The copy is what crosses over, so it has to come
    /// out in the same order and with the ages already worked out.
    /// </summary>
    [Fact]
    public void A_copy_comes_out_in_the_same_order_as_the_trail()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(10, 10, TimeSpan.Zero);
        trail.Add(200, 200, TimeSpan.FromMilliseconds(16));
        trail.Add(300, 300, TimeSpan.FromMilliseconds(32));

        var copy = new LaserPoint[10];

        Assert.Equal(3, trail.CopyTo(copy));
        Assert.Equal(trail[0].X, copy[0].X);
        Assert.Equal(trail[2].X, copy[2].X);
        Assert.Equal(trail[0].Age, copy[0].Age);
    }

    /// <summary>
    /// A copy that silently kept the first few points would draw a trail
    /// running the wrong way as the buffer filled.
    /// </summary>
    [Fact]
    public void A_copy_into_something_too_small_is_refused()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(200, 200, TimeSpan.FromMilliseconds(16));

        Assert.Throws<ArgumentException>(() => trail.CopyTo(new LaserPoint[1]));
    }

    [Fact]
    public void Clearing_leaves_nothing_behind()
    {
        var trail = new LaserTrail(Life);

        trail.Begin();
        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(200, 200, TimeSpan.FromMilliseconds(16));
        trail.Clear();

        Assert.Equal(0, trail.Count);
    }
}
