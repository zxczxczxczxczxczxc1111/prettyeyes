using Avalonia.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
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
    private CaptureRect _monitorUsable;
    private CaptureRect _selection;
    private Document? _document;
    private IAnnotation? _preview;

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
        AffectsRender<CaptureCanvas>(
            VeilOpacityProperty, FrameOpacityProperty, MagnifierAtProperty, MagnifierGridProperty);
    }

    /// <summary>
    /// Opacity of the dimming veil. Animated on open so the screen darkens
    /// instead of blinking.
    /// </summary>
    public static readonly StyledProperty<double> VeilOpacityProperty =
        AvaloniaProperty.Register<CaptureCanvas, double>(nameof(VeilOpacity));

    /// <summary>
    /// Opacity of the selection frame. Fades in with the first drag and out
    /// when the selection is dropped.
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

    /// <summary>
    /// Where the magnifier is aimed, in virtual-desktop pixels, or null when it
    /// is not wanted: no capture yet, a tool is active, or the cursor is over
    /// the toolbar.
    /// </summary>
    public static readonly StyledProperty<PixelPoint?> MagnifierAtProperty =
        AvaloniaProperty.Register<CaptureCanvas, PixelPoint?>(nameof(MagnifierAt));

    public PixelPoint? MagnifierAt
    {
        get => GetValue(MagnifierAtProperty);
        set => SetValue(MagnifierAtProperty, value);
    }

    /// <summary>
    /// The pixel grid inside the magnifier. Off turns the magnifier into plain
    /// magnification, which is what somebody looking at a picture wants rather
    /// than somebody aiming at an edge.
    /// </summary>
    public static readonly StyledProperty<bool> MagnifierGridProperty =
        AvaloniaProperty.Register<CaptureCanvas, bool>(nameof(MagnifierGrid), defaultValue: true);

    public bool MagnifierGrid
    {
        get => GetValue(MagnifierGridProperty);
        set => SetValue(MagnifierGridProperty, value);
    }

    /// <summary>
    /// The colour under the crosshair, sampled once per position rather than
    /// per frame. Null when the cursor is off the captured frame.
    /// </summary>
    public SKColor? ColorAt(int x, int y)
    {
        if (_source is null)
        {
            return null;
        }

        var local = new CaptureRect(x - _frameBounds.X, y - _frameBounds.Y, 1, 1);

        if (local.X < 0 || local.Y < 0 || local.X >= _source.Width || local.Y >= _source.Height)
        {
            return null;
        }

        using var pixels = _source.PeekPixels();

        return pixels?.GetPixelColor(local.X, local.Y);
    }

    /// <summary>
    /// Puts the veil and the frame at a value without animating there.
    ///
    /// Used when the overlay closes, where the fade out has nobody watching it
    /// and its only effect would be to leave the emptied window mid-way through
    /// a dimming when it is stored away.
    /// </summary>
    public void SnapOpacities(double veil, double frame)
    {
        var transitions = Transitions;

        Transitions = null;
        VeilOpacity = veil;
        FrameOpacity = frame;
        Transitions = transitions;
    }

    /// <summary>
    /// How bright the picture is around a point, from 0 to 1, or null when the
    /// point is off the captured frame.
    ///
    /// Around, not at: the cursor covers two dozen pixels, and a decision taken
    /// from the single pixel under its tip flips on every speck of dust in a
    /// photograph. Sampled on a coarse grid - the answer only has to be right
    /// enough to choose between two inks.
    /// </summary>
    public double? LuminanceAround(int x, int y, int reach)
    {
        if (_source is null)
        {
            return null;
        }

        using var pixels = _source.PeekPixels();

        if (pixels is null)
        {
            return null;
        }

        const int Step = 4;

        var total = 0.0;
        var counted = 0;

        for (var dy = -reach; dy <= reach; dy += Step)
        {
            for (var dx = -reach; dx <= reach; dx += Step)
            {
                var px = x + dx - _frameBounds.X;
                var py = y + dy - _frameBounds.Y;

                if (px < 0 || py < 0 || px >= _source.Width || py >= _source.Height)
                {
                    continue;
                }

                var colour = pixels.GetPixelColor(px, py);

                total += (0.2126 * colour.Red) + (0.7152 * colour.Green) + (0.0722 * colour.Blue);
                counted++;
            }
        }

        return counted == 0 ? null : total / (counted * 255.0);
    }

    public void Attach(Document document, CaptureRect monitorBounds, CaptureRect usable)
    {
        _document = document;
        _source = document.Source;
        _frameBounds = document.SourceBounds;
        _monitorBounds = monitorBounds;
        _monitorUsable = usable;
        InvalidateVisual();
    }

    /// <summary>
    /// Lets go of the captured frame. The windows outlive a capture now, and a
    /// hidden window holding 28 MB of pixels is a leak with extra steps.
    /// </summary>
    public void Detach()
    {
        _document = null;
        _source = null;
        _selection = CaptureRect.Empty;
        _preview = null;
        InvalidateVisual();
    }

    public void ShowSelection(CaptureRect selection)
    {
        _selection = selection;

        // The frame shows up with the first movement of the drag; the toolbar
        // is what waits for the gesture to end.
        if (!selection.IsEmpty && FrameOpacity < 1)
        {
            FrameOpacity = 1;
        }

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
            _document.BlurCache,
            _frameBounds,
            _monitorBounds,
            _monitorUsable,
            _selection,
            annotations,
            _preview,
            (float)(VisualRoot?.RenderScaling ?? 1.0),
            (float)VeilOpacity,
            (float)FrameOpacity,
            MagnifierAt,
            MagnifierGrid));
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

        /// <summary>
        /// 132 physical pixels showing 16 source pixels at eight times life
        /// size: enough to see a pixel, small enough not to be in the way.
        /// </summary>
        private const int MagnifierSize = 132;
        private const int MagnifierZoom = 8;
        private const int MagnifierGap = 24;
        private const float MagnifierRadius = 14;

        /// <summary>Matches the Border token: a hairline, not a lattice.</summary>
        private const byte GridAlpha = 15;

        private readonly SKImage _source;

        // The operation deliberately does not hold the document: it lives on
        // the render thread. The cache comes along as its own field because the
        // blur still needs the one that belongs to this capture.
        private readonly BlurCache _cache;

        private readonly CaptureRect _frame;
        private readonly CaptureRect _monitor;

        // Copied, not held by reference: the operation runs on the render
        // thread and must not read anything the UI thread is still editing.
        private readonly CaptureRect _usable;
        private readonly CaptureRect _selection;
        private readonly IReadOnlyList<IAnnotation> _annotations;
        private readonly IAnnotation? _preview;
        private readonly float _scaling;
        private readonly float _veilOpacity;
        private readonly float _frameOpacity;
        private readonly PixelPoint? _magnifierAt;
        private readonly bool _magnifierGrid;

        public CaptureDrawOperation(
            Rect bounds, SKImage source, BlurCache cache, CaptureRect frame, CaptureRect monitor,
            CaptureRect usable, CaptureRect selection, IReadOnlyList<IAnnotation> annotations,
            IAnnotation? preview, float scaling, float veilOpacity, float frameOpacity,
            PixelPoint? magnifierAt, bool magnifierGrid)
        {
            _magnifierAt = magnifierAt;
            _magnifierGrid = magnifierGrid;
            Bounds = bounds;
            _source = source;
            _cache = cache;
            _frame = frame;
            _monitor = monitor;
            _usable = usable;
            _selection = selection;
            _annotations = annotations;
            _preview = preview;
            _scaling = scaling;
            _veilOpacity = veilOpacity;
            _frameOpacity = frameOpacity;
        }

        public Rect Bounds { get; }

        /// <summary>
        /// A frame that misses this has been seen by the user as a stutter.
        /// 60 Hz gives 16.7 ms; the budget keeps a little for the compositor.
        /// </summary>
        private const double FrameBudgetMs = 16;

        public void Render(ImmediateDrawingContext context)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Draw(context);
            }
            finally
            {
                watch.Stop();

                // Only the frames that missed: a line per frame would bury the
                // log, and a frame nobody noticed is not worth a line.
                if (watch.Elapsed.TotalMilliseconds > FrameBudgetMs)
                {
                    Log.Default.Info($"медленный кадр: {watch.Elapsed.TotalMilliseconds:F1} мс, "
                        + $"объектов {_annotations.Count}");
                }

            }
        }

        private void Draw(ImmediateDrawingContext context)
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

            // Only the part of the frozen desktop this monitor shows. Handing
            // Skia the whole 5120x1440 image for every window on every frame
            // makes it clip the same pixels over and over.
            var visible = _monitor.Intersect(_frame);

            if (!visible.IsEmpty)
            {
                var source = SKRect.Create(
                    visible.X - _frame.X, visible.Y - _frame.Y, visible.Width, visible.Height);
                var destination = SKRect.Create(visible.X, visible.Y, visible.Width, visible.Height);

                canvas.DrawImage(_source, source, destination, SKSamplingOptions.Default, null);
            }

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
                    annotation.Draw(canvas, _source, _frame, _cache);
                }

                // The preview is not in the document, but a blur being dragged
                // is exactly the case the cache exists for, so it gets the same
                // one.
                _preview?.Draw(canvas, _source, _frame, _cache);

                canvas.Restore();

                DrawSelectionFrame(canvas, hole);
            }

            DrawMagnifier(canvas);

            canvas.Restore();
        }

        /// <summary>
        /// The pixel grid, at eight times life size. Sampling is nearest
        /// neighbour on purpose: this exists so an edge can be put on the right
        /// pixel, and a smoothed pixel has no edge to aim at.
        /// </summary>
        private void DrawMagnifier(SKCanvas canvas)
        {
            if (_magnifierAt is not { } at)
            {
                return;
            }

            var box = MagnifierPlacement.Choose(at.X, at.Y, _monitor, MagnifierSize, MagnifierGap);
            var destination = SKRect.Create(box.X, box.Y, box.Width, box.Height);

            var span = MagnifierSize / MagnifierZoom;
            var source = SKRect.Create(
                at.X - _frame.X - (span / 2f),
                at.Y - _frame.Y - (span / 2f),
                span,
                span);

            canvas.Save();

            using var round = new SKRoundRect(destination, MagnifierRadius);
            canvas.ClipRoundRect(round, antialias: true);

            // Anything outside the captured frame has no pixels to show: black
            // is honest, a stretched edge pixel is not.
            using var backdrop = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(destination, backdrop);

            var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
            canvas.DrawImage(_source, source, destination, sampling, null);

            if (_magnifierGrid)
            {
                DrawPixelGrid(canvas, destination);
            }

            DrawCrosshair(canvas, destination);

            canvas.Restore();

            // The rim goes on outside the clip, so it is not cut in half.
            using var rim = Stroke(new SKColor(255, 255, 255, FrameLightAlpha));
            using var rimRound = new SKRoundRect(destination, MagnifierRadius);
            canvas.DrawRoundRect(rimRound, rim);
        }

        private static void DrawPixelGrid(SKCanvas canvas, SKRect box)
        {
            using var line = new SKPaint
            {
                Color = new SKColor(255, 255, 255, GridAlpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
            };

            for (var offset = MagnifierZoom; offset < MagnifierSize; offset += MagnifierZoom)
            {
                canvas.DrawLine(box.Left + offset, box.Top, box.Left + offset, box.Bottom, line);
                canvas.DrawLine(box.Left, box.Top + offset, box.Right, box.Top + offset, line);
            }
        }

        /// <summary>
        /// The centre cell, outlined with the same double hairline as the
        /// selection frame so it reads on any wallpaper.
        /// </summary>
        private static void DrawCrosshair(SKCanvas canvas, SKRect box)
        {
            var centre = SKRect.Create(
                box.Left + ((MagnifierSize - MagnifierZoom) / 2f),
                box.Top + ((MagnifierSize - MagnifierZoom) / 2f),
                MagnifierZoom,
                MagnifierZoom);

            using var dark = Stroke(new SKColor(0, 0, 0, FrameDarkAlpha));
            using var light = Stroke(new SKColor(255, 255, 255, FrameLightAlpha));

            var outer = centre;
            outer.Inflate(0.5f, 0.5f);
            canvas.DrawRect(outer, dark);
            canvas.DrawRect(centre, light);
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

            // A selection dragged to the very bottom used to draw its last line
            // underneath the shell's bar, where nobody can see it. The pixels
            // are captured from rcMonitor either way; only the line moves up.
            var usable = _usable.IsEmpty ? _monitor : _usable;
            hole.Bottom = Math.Min(
                hole.Bottom,
                FrameEdge.Bottom((int)hole.Bottom, usable.Bottom, _monitor.Bottom));

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
