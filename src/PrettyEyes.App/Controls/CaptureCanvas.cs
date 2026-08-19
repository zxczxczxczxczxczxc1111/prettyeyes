using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PrettyEyes.Core.Model;
using SkiaSharp;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Controls;

/// <summary>
/// Draws the frozen screen, the dimming veil and the current selection for one
/// monitor.
/// </summary>
public sealed class CaptureCanvas : Control
{
    private SKImage? _source;
    private CaptureRect _frameBounds;
    private CaptureRect _monitorBounds;
    private CaptureRect _selection;
    private Document? _document;
    private IAnnotation? _preview;

    public void Attach(Document document, CaptureRect monitorBounds)
    {
        _document = document;
        _source = document.Source;
        _frameBounds = document.SourceBounds;
        _monitorBounds = monitorBounds;
        InvalidateVisual();
    }

    public void ShowSelection(CaptureRect selection)
    {
        _selection = selection;
        InvalidateVisual();
    }

    /// <summary>
    /// The shape the active tool would produce if the gesture ended now. Drawn
    /// on top of the stored annotations and never added to the document.
    /// </summary>
    public void ShowPreview(IAnnotation? annotation)
    {
        _preview = annotation;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_source is null || _document is null)
        {
            return;
        }

        // Snapshot on the UI thread: the draw operation runs on the render one.
        var annotations = _document.SnapshotAnnotations();

        context.Custom(new CaptureDrawOperation(
            new Rect(Bounds.Size),
            _source,
            _frameBounds,
            _monitorBounds,
            _selection,
            annotations,
            _preview,
            (float)(VisualRoot?.RenderScaling ?? 1.0)));
    }

    private sealed class CaptureDrawOperation : ICustomDrawOperation
    {
        private const byte VeilAlpha = 140;
        private const byte FrameAlpha = 235;
        private const float HandleRadius = 3f;

        private readonly SKImage _source;
        private readonly CaptureRect _frame;
        private readonly CaptureRect _monitor;
        private readonly CaptureRect _selection;
        private readonly IReadOnlyList<IAnnotation> _annotations;
        private readonly IAnnotation? _preview;
        private readonly float _scaling;

        public CaptureDrawOperation(
            Rect bounds, SKImage source, CaptureRect frame, CaptureRect monitor,
            CaptureRect selection, IReadOnlyList<IAnnotation> annotations,
            IAnnotation? preview, float scaling)
        {
            Bounds = bounds;
            _source = source;
            _frame = frame;
            _monitor = monitor;
            _selection = selection;
            _annotations = annotations;
            _preview = preview;
            _scaling = scaling;
        }

        public Rect Bounds { get; }

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

            // The lease hands over a canvas that already carries the DPI scale.
            // Undo it: everything below is in physical pixels.
            canvas.Scale(1f / _scaling);
            canvas.Translate(-_monitor.X, -_monitor.Y);

            canvas.DrawImage(_source, _frame.X, _frame.Y);

            using var veil = new SKPaint { Color = new SKColor(0, 0, 0, VeilAlpha) };
            var monitorRect = SKRect.Create(_monitor.X, _monitor.Y, _monitor.Width, _monitor.Height);

            if (_selection.IsEmpty)
            {
                canvas.DrawRect(monitorRect, veil);
            }
            else
            {
                var hole = SKRect.Create(_selection.X, _selection.Y, _selection.Width, _selection.Height);

                canvas.Save();
                canvas.ClipRect(hole, SKClipOperation.Difference);
                canvas.DrawRect(monitorRect, veil);
                canvas.Restore();

                foreach (var annotation in _annotations)
                {
                    annotation.Draw(canvas, _source, _frame);
                }

                _preview?.Draw(canvas, _source, _frame);

                DrawSelectionFrame(canvas);
            }

            canvas.Restore();
        }

        /// <summary>
        /// White 0.92 rather than the 6% border from the token set: this frame
        /// sits on an arbitrary screenshot, where a 6% line simply vanishes.
        /// The one deliberate departure from the design language.
        /// </summary>
        private void DrawSelectionFrame(SKCanvas canvas)
        {
            var white = new SKColor(255, 255, 255, FrameAlpha);

            using var stroke = new SKPaint
            {
                Color = white,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = false,
            };

            canvas.DrawRect(
                SKRect.Create(_selection.X, _selection.Y, _selection.Width, _selection.Height),
                stroke);

            using var handle = new SKPaint
            {
                Color = white,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };

            canvas.DrawCircle(_selection.X, _selection.Y, HandleRadius, handle);
            canvas.DrawCircle(_selection.Right, _selection.Y, HandleRadius, handle);
            canvas.DrawCircle(_selection.X, _selection.Bottom, HandleRadius, handle);
            canvas.DrawCircle(_selection.Right, _selection.Bottom, HandleRadius, handle);
        }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }
}
