using System.Buffers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Laser;
using SkiaSharp;

namespace PrettyEyes.App.Controls;

/// <summary>
/// Draws a laser trail over one monitor, and nothing else.
///
/// Deliberately knows nothing about documents, selections or annotations: the
/// same layer sits over the capture overlay and over the bare desktop, and the
/// only difference between the two is which window it is in.
/// </summary>
public sealed class LaserLayer : Control, IDisposable
{
    private readonly LaserPainter _painter = new();

    public LaserLayer()
    {
        // The trail is looked at, never touched. Without this the overlay stops
        // being able to draw where the pointer has just been.
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Shared with every other monitor's layer: one pointer leaves one trail,
    /// and a trail per window would restart at every monitor boundary.
    /// </summary>
    public LaserTrail? Trail { get; set; }

    /// <summary>Where this layer's monitor sits on the virtual desktop, in physical pixels.</summary>
    public CaptureRect Monitor { get; set; }

    public SKColor Colour { get; set; } = LaserPainter.Default;

    /// <summary>
    /// Whether any of the trail is over this monitor.
    ///
    /// One trail, a pane per monitor, and a stroke drawn on one of them is
    /// nothing at all on the others. Repainting those anyway costs a present
    /// of a whole second screen per frame, which on a two-monitor desk is half
    /// the work done for nothing.
    /// </summary>
    public bool Wanted()
    {
        if (Trail is not { Count: > 0 } trail)
        {
            return false;
        }

        for (var index = 0; index < trail.Count; index++)
        {
            var point = trail[index];

            if (point.X >= Monitor.X - Spill
                && point.X <= Monitor.Right + Spill
                && point.Y >= Monitor.Y - Spill
                && point.Y <= Monitor.Bottom + Spill)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Comfortably wider than the beam, so its edge is never clipped at a boundary.</summary>
    private const int Spill = 24;

    /// <summary>
    /// How many frames this layer has actually drawn. Asking for a repaint and
    /// getting one are different things, and the difference is the whole
    /// question when a full-screen pane feels slow.
    /// </summary>
    public long Repaints { get; private set; }

    public override void Render(DrawingContext context)
    {
        if (Trail is not { Count: > 0 } trail)
        {
            return;
        }

        Repaints++;

        // Rented rather than allocated: this runs on every frame while
        // somebody is presenting, and the project counts what drawing costs in
        // tenths of a kilobyte.
        var points = ArrayPool<LaserPoint>.Shared.Rent(trail.Count);
        var count = trail.CopyTo(points);

        context.Custom(new LaserDrawOperation(
            new Rect(Bounds.Size),
            _painter,
            points,
            count,
            Colour,
            Monitor,
            (float)(VisualRoot?.RenderScaling ?? 1.0)));
    }

    public void Dispose() => _painter.Dispose();

    private sealed class LaserDrawOperation : ICustomDrawOperation
    {
        private readonly LaserPainter _painter;
        private readonly LaserPoint[] _points;
        private readonly int _count;
        private readonly SKColor _colour;
        private readonly CaptureRect _monitor;
        private readonly float _scaling;

        public LaserDrawOperation(
            Rect bounds, LaserPainter painter, LaserPoint[] points, int count,
            SKColor colour, CaptureRect monitor, float scaling)
        {
            Bounds = bounds;
            _painter = painter;
            _points = points;
            _count = count;
            _colour = colour;
            _monitor = monitor;
            _scaling = scaling;
        }

        public Rect Bounds { get; }

        /// <summary>Nothing here is ever clicked; the layer is not hit-testable either.</summary>
        public bool HitTest(Point point) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();

            if (lease is null)
            {
                return;
            }

            using var api = lease.Lease();
            var canvas = api.SkCanvas;

            canvas.Save();

            // The lease hands over a canvas carrying the DPI scale. Undone, so
            // the points can stay in the physical pixels the cursor is
            // reported in - the same convention CaptureCanvas draws under.
            canvas.Scale(1f / _scaling);
            canvas.Translate(-_monitor.X, -_monitor.Y);

            _painter.Paint(canvas, _points.AsSpan(0, _count), _colour);

            canvas.Restore();
        }

        public void Dispose() => ArrayPool<LaserPoint>.Shared.Return(_points);
    }
}
