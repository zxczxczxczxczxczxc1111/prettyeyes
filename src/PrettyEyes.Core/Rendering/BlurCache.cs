using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Blurred slices of the captured screen, kept by region.
///
/// Blur is the only annotation whose cost is not a rounding error: measured at
/// about 1.5 ms per region, redone on every rendered frame, while dragging a
/// frame renders as fast as the mouse reports. Caching inside the annotation
/// would not help - the blur tool builds a brand new annotation on every
/// pointer move - so the cache belongs to the render side and is keyed by what
/// actually decides the pixels.
///
/// Entries are raster images: the same one is drawn by the overlay on the
/// render thread and by the exporter on the UI thread. That is also why every
/// access is behind a lock.
///
/// One cache per document, not one per process. Two documents alive at once -
/// a pinned window and a fresh overlay - would otherwise share a sixteen-entry
/// budget and evict each other's pixels.
/// </summary>
public sealed class BlurCache : IDisposable
{
    /// <summary>
    /// Enough for a screenshot full of blurred regions, small enough that a
    /// long editing session cannot grow it without bound. Each entry holds
    /// pixels for its own region only.
    /// </summary>
    private const int Capacity = 16;

    private readonly object _gate = new();

    // Insertion-ordered: the first key is the oldest, which is the one to drop.
    private readonly Dictionary<Key, SKImage> _entries = [];

    private readonly List<Key> _order = [];

    /// <summary>
    /// How many times a miss forced real work. Public because the project has
    /// no InternalsVisibleTo, and because a cache whose hit rate cannot be seen
    /// is a cache nobody can trust.
    /// </summary>
    public int Computed { get; private set; }

    /// <summary>
    /// The blurred slice for this region, computed once. The factory runs under
    /// the lock, so two threads asking for the same region wait rather than
    /// both doing the work.
    /// </summary>
    public SKImage Get(SKImage source, CaptureRect region, float sigma, Func<SKImage> build)
    {
        var key = new Key(source.UniqueId, region, sigma);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var image = build();
            Computed++;

            _entries[key] = image;
            _order.Add(key);

            while (_order.Count > Capacity)
            {
                var oldest = _order[0];
                _order.RemoveAt(0);

                // Dropped, not disposed: somebody may still be drawing with
                // this slice, and eviction is a memory decision rather than
                // permission to destroy pixels that are in use. The finalizer
                // collects it once nobody holds it.
                _entries.Remove(oldest);
            }

            return image;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var image in _entries.Values)
            {
                image.Dispose();
            }

            _entries.Clear();
            _order.Clear();
        }
    }

    public void Dispose() => Clear();

    /// <summary>
    /// Everything the pixels depend on: which capture, which region, how
    /// strong. The capture's own id keeps the entries of an old screenshot from
    /// ever being handed to a new one.
    /// </summary>
    private readonly record struct Key(uint Source, CaptureRect Region, float Sigma);
}
