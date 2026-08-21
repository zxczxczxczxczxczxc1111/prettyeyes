using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Controls;

/// <summary>
/// One pinned frame, drawn at whatever zoom the window is at.
///
/// A separate control rather than a reuse of <see cref="CaptureCanvas"/>: that
/// one is nailed to a monitor - it undoes the DPI scale, translates by the
/// monitor's corner and hit-tests from there - and it paints a veil, a
/// selection frame and a magnifier. None of that means anything here.
///
/// Everything is measured in <b>frame pixels</b>. Thickness, glyph size and hit
/// testing are the same numbers that end up in the file; zoom only decides how
/// large they look.
/// </summary>
public sealed class PinnedCanvas : Control
{
    private Document? _document;
    private IAnnotation? _preview;
    private double _zoom = 1;

    /// <summary>What the window shows. Its own, not shared with the overlay.</summary>
    public Document? Document
    {
        get => _document;
        set
        {
            _document = value;
            InvalidateVisual();
        }
    }

    /// <summary>1 is life size in frame pixels.</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            InvalidateVisual();
        }
    }

    /// <summary>The half-finished shape under the pointer.</summary>
    public void ShowPreview(IAnnotation? preview)
    {
        _preview = preview;
        InvalidateVisual();
    }

    /// <summary>
    /// A point in this control to a pixel of the frame.
    ///
    /// Counted from the document's own bounds rather than from where the window
    /// happens to sit: the window is dragged around the desktop all day, and
    /// the annotations must not move with it.
    /// </summary>
    public (int X, int Y) ToFramePixels(Point point)
    {
        var frame = _document?.SourceBounds ?? CaptureRect.Empty;
        var scale = _zoom / Scaling;

        return (
            frame.X + (int)Math.Round(point.X / scale),
            frame.Y + (int)Math.Round(point.Y / scale));
    }

    /// <summary>Size in device-independent units for the current zoom.</summary>
    public Size Wanted()
    {
        var frame = _document?.SourceBounds ?? CaptureRect.Empty;
        var scale = _zoom / Scaling;

        return new Size(frame.Width * scale, frame.Height * scale);
    }

    private double Scaling => VisualRoot?.RenderScaling ?? 1.0;

    public override void Render(DrawingContext context)
    {
        if (_document is null)
        {
            return;
        }

        // Snapshot on the UI thread: the draw operation runs on the render one.
        context.Custom(new PinnedDrawOperation(
            new Rect(Bounds.Size),
            _document.Source,
            _document.BlurCache,
            _document.SourceBounds,
            _document.SnapshotAnnotations(),
            _preview,
            (float)(_zoom / Scaling)));
    }

    private sealed class PinnedDrawOperation : ICustomDrawOperation
    {
        private readonly SKImage _source;
        private readonly BlurCache _cache;
        private readonly CaptureRect _frame;
        private readonly IReadOnlyList<IAnnotation> _annotations;
        private readonly IAnnotation? _preview;
        private readonly float _scale;

        public PinnedDrawOperation(
            Rect bounds, SKImage source, BlurCache cache, CaptureRect frame,
            IReadOnlyList<IAnnotation> annotations, IAnnotation? preview, float scale)
        {
            Bounds = bounds;
            _source = source;
            _cache = cache;
            _frame = frame;
            _annotations = annotations;
            _preview = preview;
            _scale = scale;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point point) => true;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

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

            // The lease hands over a canvas carrying the DPI scale; from here
            // on the units are frame pixels, and zoom is the only scale.
            canvas.Scale(_scale);
            canvas.Translate(-_frame.X, -_frame.Y);

            canvas.DrawImage(_source, _frame.X, _frame.Y);

            foreach (var annotation in _annotations)
            {
                annotation.Draw(canvas, _source, _frame, _cache);
            }

            _preview?.Draw(canvas, _source, _frame, _cache);

            canvas.Restore();
        }
    }
}
