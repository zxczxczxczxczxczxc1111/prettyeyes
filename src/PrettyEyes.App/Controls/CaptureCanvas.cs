using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
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
    private bool _showHandles;

    public CaptureCanvas()
    {
        // Durations come from the design tokens: nothing in this app animates
        // for longer than 200 ms.
        Transitions =
        [
            new DoubleTransition
            {
                Property = VeilOpacityProperty,
                Duration = TimeSpan.FromMilliseconds(180),
                Easing = new CubicEaseOut(),
            },
            new DoubleTransition
            {
                Property = FrameOpacityProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    static CaptureCanvas()
    {
        AffectsRender<CaptureCanvas>(VeilOpacityProperty, FrameOpacityProperty);
    }

    /// <summary>
    /// Opacity of the dimming veil. Animated on open so the screen darkens
    /// instead of blinking.
    /// </summary>
    public static readonly StyledProperty<double> VeilOpacityProperty =
        AvaloniaProperty.Register<CaptureCanvas, double>(nameof(VeilOpacity));

    /// <summary>
    /// Opacity of the frame and its handles. They appear when the gesture ends,
    /// not while the pointer is still down.
    /// </summary>
    public static readonly StyledProperty<double> FrameOpacityProperty =
        AvaloniaProperty.Register<CaptureCanvas, double>(nameof(FrameOpacity));

    public double VeilOpacity
    {
        get => GetValue(VeilOpacityProperty);
        set => SetValue(VeilOpacityProperty, value);
    }

    public double FrameOpacity
    {
        get => GetValue(FrameOpacityProperty);
        set => SetValue(FrameOpacityProperty, value);
    }

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

        // While a region is being dragged the frame is drawn straight away;
        // only the handles wait for the gesture to end.
        if (!selection.IsEmpty && FrameOpacity < 1)
        {
            FrameOpacity = 1;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Handles mark a selection that can be grabbed, so they only make sense
    /// once the pointer is up.
    /// </summary>
    public void ShowHandles(bool show)
    {
        _showHandles = show;
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
            (float)(VisualRoot?.RenderScaling ?? 1.0),
            (float)VeilOpacity,
            (float)FrameOpacity,
            _showHandles));
    }

    private sealed class CaptureDrawOperation : ICustomDrawOperation
    {
        /// <summary>
        /// Veil at 0.50: dark enough to push the screen back, light enough that
        /// the user still sees what they are aiming at.
        /// </summary>
        private const byte VeilAlpha = 128;

        // Double hairline. A single white line vanishes on a white document and
        // a single dark one vanishes on a dark editor; together they read on
        // anything.
        private const byte FrameDarkAlpha = 89;
        private const byte FrameLightAlpha = 204;

        /// <summary>
        /// The one flourish: an inset highlight that makes the cutout look like
        /// a pane of glass lifted above the dimmed screen.
        /// </summary>
        private const byte GlassAlpha = 26;

        private const byte HandleAlpha = 235;
        private const float HandleRadius = 4f;

        private readonly SKImage _source;
        private readonly CaptureRect _frame;
        private readonly CaptureRect _monitor;
        private readonly CaptureRect _selection;
        private readonly IReadOnlyList<IAnnotation> _annotations;
        private readonly IAnnotation? _preview;
        private readonly float _scaling;
        private readonly float _veilOpacity;
        private readonly float _frameOpacity;
        private readonly bool _showHandles;

        public CaptureDrawOperation(
            Rect bounds, SKImage source, CaptureRect frame, CaptureRect monitor,
            CaptureRect selection, IReadOnlyList<IAnnotation> annotations,
            IAnnotation? preview, float scaling, float veilOpacity, float frameOpacity,
            bool showHandles)
        {
            Bounds = bounds;
            _source = source;
            _frame = frame;
            _monitor = monitor;
            _selection = selection;
            _annotations = annotations;
            _preview = preview;
            _scaling = scaling;
            _veilOpacity = veilOpacity;
            _frameOpacity = frameOpacity;
            _showHandles = showHandles;
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

            using var veil = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(VeilAlpha * _veilOpacity)) };
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

                // Everything a tool draws belongs to the selection: outside it
                // the pixels are never saved, so showing them is a lie.
                canvas.Save();
                canvas.ClipRect(hole);

                foreach (var annotation in _annotations)
                {
                    annotation.Draw(canvas, _source, _frame);
                }

                _preview?.Draw(canvas, _source, _frame);

                canvas.Restore();

                DrawSelectionFrame(canvas, hole);
            }

            canvas.Restore();
        }

        private void DrawSelectionFrame(SKCanvas canvas, SKRect hole)
        {
            if (_frameOpacity <= 0.01f)
            {
                return;
            }

            using var dark = Stroke(new SKColor(0, 0, 0, Scaled(FrameDarkAlpha)));
            using var light = Stroke(new SKColor(255, 255, 255, Scaled(FrameLightAlpha)));
            using var glass = Stroke(new SKColor(255, 255, 255, Scaled(GlassAlpha)));

            // Half-pixel offsets: a 1px stroke centred on an integer coordinate
            // lands on two pixel rows and turns grey.
            var outer = hole;
            outer.Inflate(0.5f, 0.5f);
            canvas.DrawRect(outer, dark);

            var inner = hole;
            inner.Inflate(-0.5f, -0.5f);
            canvas.DrawRect(inner, light);

            var glassEdge = hole;
            glassEdge.Inflate(-1.5f, -1.5f);
            canvas.DrawRect(glassEdge, glass);

            if (!_showHandles)
            {
                return;
            }

            using var fill = new SKPaint
            {
                Color = new SKColor(255, 255, 255, Scaled(HandleAlpha)),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };

            using var ring = Stroke(new SKColor(0, 0, 0, Scaled(FrameDarkAlpha)));

            var midX = hole.Left + (hole.Width / 2f);
            var midY = hole.Top + (hole.Height / 2f);

            foreach (var (x, y) in new[]
            {
                (hole.Left, hole.Top), (midX, hole.Top), (hole.Right, hole.Top),
                (hole.Right, midY), (hole.Right, hole.Bottom), (midX, hole.Bottom),
                (hole.Left, hole.Bottom), (hole.Left, midY),
            })
            {
                canvas.DrawCircle(x, y, HandleRadius, fill);
                canvas.DrawCircle(x, y, HandleRadius, ring);
            }
        }

        private byte Scaled(byte alpha) => (byte)(alpha * _frameOpacity);

        private static SKPaint Stroke(SKColor color) => new()
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true,
        };

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }
}
