using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Capture;

/// <summary>Where one monitor starts inside the desktop buffer, in bytes.</summary>
public readonly record struct FramePlacement(MonitorInfo Monitor, long Offset);

/// <summary>
/// The arithmetic that turns a set of monitors into one buffer: how long a row
/// is, how much memory the desktop needs, whether it has to be cleared first,
/// and where each monitor begins.
///
/// Lives in Core and not next to a capture engine on purpose. There is more
/// than one engine now, this is the part of them that can be tested, and it is
/// exactly the sort of arithmetic that grows a second slightly different copy
/// the moment it is written twice.
/// </summary>
public sealed class DesktopFrameLayout
{
    private DesktopFrameLayout(
        CaptureRect bounds,
        int stride,
        nuint size,
        bool needsZeroing,
        IReadOnlyList<FramePlacement> placements)
    {
        Bounds = bounds;
        Stride = stride;
        Size = size;
        NeedsZeroing = needsZeroing;
        Placements = placements;
    }

    /// <summary>The whole virtual desktop. X and Y can be negative.</summary>
    public CaptureRect Bounds { get; }

    /// <summary>Bytes per row of the buffer, four per pixel.</summary>
    public int Stride { get; }

    public nuint Size { get; }

    /// <summary>
    /// True when the monitors leave part of the bounding box unpainted. An
    /// unpainted byte is not black, it is whatever the last tenant of that
    /// memory left there.
    /// </summary>
    public bool NeedsZeroing { get; }

    public IReadOnlyList<FramePlacement> Placements { get; }

    public static DesktopFrameLayout For(DesktopLayout layout)
    {
        var bounds = layout.VirtualBounds;

        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Virtual desktop reported a non-positive size.");
        }

        if (bounds.Width > int.MaxValue / 4)
        {
            throw new InvalidOperationException(
                $"Virtual desktop is too wide for a 32-bit row stride ({bounds.Width} pixels).");
        }

        var stride = bounds.Width * 4;
        var size = (nuint)((long)stride * bounds.Height);

        var placements = layout.Monitors
            .Select(monitor => new FramePlacement(
                monitor,
                ((monitor.Bounds.Y - bounds.Y) * (long)stride)
                    + ((monitor.Bounds.X - bounds.X) * 4L)))
            .ToArray();

        return new DesktopFrameLayout(bounds, stride, size, NeedsClearing(layout), placements);
    }

    /// <summary>
    /// Whether the monitors cover the bounding box between them.
    ///
    /// Areas are added up and the pairwise overlaps taken back out. That is the
    /// exact answer for two monitors and an underestimate for three that all
    /// overlap each other, which errs towards clearing a buffer that did not
    /// need it. The older code just added the areas up, so two monitors
    /// overlapping by the size of the gap next door came out as "no gaps" and
    /// the gap kept whatever was in that memory before.
    /// </summary>
    private static bool NeedsClearing(DesktopLayout layout)
    {
        var monitors = layout.Monitors;
        var covered = monitors.Sum(monitor => (long)monitor.Bounds.Width * monitor.Bounds.Height);

        for (var i = 0; i < monitors.Count; i++)
        {
            for (var j = i + 1; j < monitors.Count; j++)
            {
                var shared = monitors[i].Bounds.Intersect(monitors[j].Bounds);
                covered -= (long)shared.Width * shared.Height;
            }
        }

        var whole = (long)layout.VirtualBounds.Width * layout.VirtualBounds.Height;

        return covered < whole;
    }
}
