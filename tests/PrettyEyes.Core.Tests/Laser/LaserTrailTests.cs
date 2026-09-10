using PrettyEyes.Core.Laser;
using Xunit;

namespace PrettyEyes.Core.Tests.Laser;

/// <summary>
/// The tail behind the pointer: what is in it, in what order, and when a point
/// stops counting.
///
/// All of it is arithmetic over a fixed buffer, which is the point - the thing
/// runs sixty times a second over a live screen, and none of that is a place to
/// find out that the oldest point was the one being drawn as the head.
/// </summary>
public class LaserTrailTests
{
    private static readonly TimeSpan Life = TimeSpan.FromMilliseconds(900);

    [Fact]
    public void A_fresh_trail_has_nothing_in_it()
    {
        Assert.Equal(0, new LaserTrail(Life).Count);
    }

    [Fact]
    public void A_point_put_in_is_a_point_that_comes_out()
    {
        var trail = new LaserTrail(Life);

        trail.Add(10, 20, TimeSpan.Zero);

        Assert.Equal(1, trail.Count);
        Assert.Equal(10, trail[0].X);
        Assert.Equal(20, trail[0].Y);
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

        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(2, 2, TimeSpan.FromMilliseconds(16));
        trail.Add(3, 3, TimeSpan.FromMilliseconds(32));

        Assert.Equal(3, trail.Count);
        Assert.Equal(1, trail[0].X);
        Assert.Equal(3, trail[2].X);
    }

    [Fact]
    public void Age_runs_from_the_tail_down_to_nothing_at_the_head()
    {
        var trail = new LaserTrail(Life);

        for (var tick = 0; tick < 10; tick++)
        {
            trail.Add(tick, 0, TimeSpan.FromMilliseconds(tick * 16));
        }

        var now = TimeSpan.FromMilliseconds(9 * 16);

        trail.Advance(now);

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

        trail.Add(0, 0, TimeSpan.Zero);
        trail.Advance(Life / 2);

        Assert.Equal(0.5, trail[0].Age, 2);
    }

    [Fact]
    public void A_point_past_its_life_is_gone()
    {
        var trail = new LaserTrail(Life);

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

        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(2, 2, Life / 2);
        trail.Advance(Life + TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, trail.Count);
        Assert.Equal(2, trail[0].X);
    }

    [Fact]
    public void The_buffer_never_grows_past_what_it_was_given()
    {
        var trail = new LaserTrail(Life, capacity: 8);

        for (var tick = 0; tick < 40; tick++)
        {
            trail.Add(tick, 0, TimeSpan.FromMilliseconds(tick));
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

        for (var tick = 0; tick < 40; tick++)
        {
            trail.Add(tick, 0, TimeSpan.FromMilliseconds(tick));
        }

        Assert.Equal(39, trail[7].X);
        Assert.Equal(32, trail[0].X);
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

        trail.Add(400, 300, TimeSpan.Zero);

        for (var tick = 1; tick < 120; tick++)
        {
            trail.Add(400, 300, TimeSpan.FromMilliseconds(tick * 16));
        }

        Assert.Equal(0, trail.Count);
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

        trail.Add(0, 0, TimeSpan.Zero);
        trail.Add(3800, 2000, TimeSpan.FromMilliseconds(16));

        Assert.Equal(2, trail.Count);
        Assert.Equal(3800, trail[1].X);
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

        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(2, 2, TimeSpan.FromMilliseconds(16));
        trail.Add(3, 3, TimeSpan.FromMilliseconds(32));

        var copy = new LaserPoint[10];

        Assert.Equal(3, trail.CopyTo(copy));
        Assert.Equal(1, copy[0].X);
        Assert.Equal(3, copy[2].X);
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

        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(2, 2, TimeSpan.FromMilliseconds(16));

        Assert.Throws<ArgumentException>(() => trail.CopyTo(new LaserPoint[1]));
    }

    [Fact]
    public void Clearing_leaves_nothing_behind()
    {
        var trail = new LaserTrail(Life);

        trail.Add(1, 1, TimeSpan.Zero);
        trail.Add(2, 2, TimeSpan.FromMilliseconds(16));
        trail.Clear();

        Assert.Equal(0, trail.Count);
    }
}
