namespace PrettyEyes.Core.Geometry;

/// <summary>
/// All monitors as one coordinate space.
/// </summary>
public sealed class DesktopLayout
{
    public DesktopLayout(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("Desktop layout needs at least one monitor.", nameof(monitors));
        }

        Monitors = monitors;
        VirtualBounds = Union(monitors);
    }

    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>
    /// Bounding box of every monitor. X and Y are not necessarily zero.
    /// </summary>
    public CaptureRect VirtualBounds { get; }

    public MonitorInfo? MonitorAt(int x, int y) =>
        Monitors.FirstOrDefault(m => m.Bounds.Contains(x, y));

    public CaptureRect ToMonitorLocal(MonitorInfo monitor, CaptureRect rect) =>
        new(rect.X - monitor.Bounds.X, rect.Y - monitor.Bounds.Y, rect.Width, rect.Height);

    private static CaptureRect Union(IReadOnlyList<MonitorInfo> monitors)
    {
        var left = monitors.Min(m => m.Bounds.X);
        var top = monitors.Min(m => m.Bounds.Y);
        var right = monitors.Max(m => m.Bounds.Right);
        var bottom = monitors.Max(m => m.Bounds.Bottom);

        return new CaptureRect(left, top, right - left, bottom - top);
    }
}
