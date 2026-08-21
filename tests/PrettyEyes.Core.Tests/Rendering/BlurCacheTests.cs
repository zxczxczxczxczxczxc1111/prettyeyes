using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class BlurCacheTests
{
    private static SKImage NewSource(SKColor colour)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(colour);

        return surface.Snapshot();
    }

    private static SKImage NewSlice()
    {
        using var surface = SKSurface.Create(new SKImageInfo(10, 10));
        surface.Canvas.Clear(SKColors.Gray);

        return surface.Snapshot();
    }

    [Fact]
    public void Second_request_for_the_same_region_is_not_recomputed()
    {
        using var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);
        var region = new CaptureRect(10, 10, 40, 40);

        var first = cache.Get(source, region, 8f, NewSlice);
        var second = cache.Get(source, region, 8f, NewSlice);

        Assert.Same(first, second);
        Assert.Equal(1, cache.Computed);
    }

    [Fact]
    public void A_moved_region_is_computed_again()
    {
        using var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);

        cache.Get(source, new CaptureRect(10, 10, 40, 40), 8f, NewSlice);
        cache.Get(source, new CaptureRect(11, 10, 40, 40), 8f, NewSlice);

        Assert.Equal(2, cache.Computed);
    }

    [Fact]
    public void A_different_strength_is_computed_again()
    {
        using var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);
        var region = new CaptureRect(10, 10, 40, 40);

        cache.Get(source, region, 8f, NewSlice);
        cache.Get(source, region, 12f, NewSlice);

        Assert.Equal(2, cache.Computed);
    }

    [Fact]
    public void A_new_capture_never_gets_the_previous_ones_pixels()
    {
        using var cache = new BlurCache();
        using var first = NewSource(SKColors.Blue);
        using var second = NewSource(SKColors.Red);
        var region = new CaptureRect(10, 10, 40, 40);

        var fromFirst = cache.Get(first, region, 8f, NewSlice);
        var fromSecond = cache.Get(second, region, 8f, NewSlice);

        Assert.NotSame(fromFirst, fromSecond);
        Assert.Equal(2, cache.Computed);
    }

    [Fact]
    public void The_cache_stays_bounded_and_drops_the_oldest_first()
    {
        using var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);

        // One past the capacity of 16.
        for (var i = 0; i < 17; i++)
        {
            cache.Get(source, new CaptureRect(i, 0, 40, 40), 8f, NewSlice);
        }

        // The first region was evicted, so asking for it again is real work.
        cache.Get(source, new CaptureRect(0, 0, 40, 40), 8f, NewSlice);

        Assert.Equal(18, cache.Computed);

        // The newest one is still there.
        cache.Get(source, new CaptureRect(16, 0, 40, 40), 8f, NewSlice);

        Assert.Equal(18, cache.Computed);
    }

    [Fact]
    public void Clear_lets_go_of_everything()
    {
        var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);
        var region = new CaptureRect(10, 10, 40, 40);

        cache.Get(source, region, 8f, NewSlice);
        cache.Clear();
        cache.Get(source, region, 8f, NewSlice);

        Assert.Equal(2, cache.Computed);

        cache.Dispose();
    }

    [Fact]
    public void Eviction_lets_go_of_the_image_instead_of_disposing_it()
    {
        // A pinned window can still be drawing with a slice that fell out of
        // the cache: eviction is a memory decision, not permission to destroy
        // pixels somebody else is holding. Disposing here is how a live window
        // lost its image mid-frame, in silence.
        using var cache = new BlurCache();
        using var source = NewSource(SKColors.Blue);

        var oldest = cache.Get(source, new CaptureRect(0, 0, 40, 40), 8f, NewSlice);

        // Capacity is sixteen, so sixteen more distinct regions push it out.
        for (var i = 1; i <= 16; i++)
        {
            cache.Get(source, new CaptureRect(i, 0, 40, 40), 8f, NewSlice);
        }

        // IsDisposed is protected internal on SKNativeObject and invisible from
        // here; the handle is the same fact said out loud.
        Assert.NotEqual(IntPtr.Zero, oldest.Handle);

        oldest.Dispose();
    }

    [Fact]
    public void One_documents_cache_does_not_evict_another_documents_image()
    {
        // Guard rather than driver: separate instances are already separate.
        // What it pins down is that nothing goes back to a process-wide cache,
        // which is what made a pinned window and a live overlay share one
        // sixteen-entry budget.
        using var mine = new BlurCache();
        using var theirs = new BlurCache();
        using var source = NewSource(SKColors.Blue);
        var region = new CaptureRect(10, 10, 40, 40);

        var kept = theirs.Get(source, region, 8f, NewSlice);

        // Different regions on purpose: sixty-four hits on one key evict nothing.
        for (var i = 0; i < 64; i++)
        {
            mine.Get(source, new CaptureRect(i, 0, 40, 40), 8f, NewSlice);
        }

        Assert.Same(kept, theirs.Get(source, region, 8f, NewSlice));
        Assert.Equal(1, theirs.Computed);
    }
}
