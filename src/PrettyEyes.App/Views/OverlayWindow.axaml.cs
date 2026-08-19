using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Views;

public partial class OverlayWindow : Window
{
    private const double Gap = 8;

    private CaptureRect _monitorBounds;
    private int _anchorX;
    private int _anchorY;
    private bool _dragging;
    private OverlayMode _mode = OverlayMode.Selecting;
    private ITool? _tool;

    public OverlayWindow() => InitializeComponent();

    public event EventHandler<CaptureRect>? SelectionChanged;

    public event EventHandler? Cancelled;

    public event EventHandler? UndoRequested;

    /// <summary>Raised once a tool gesture produced a shape.</summary>
    public event EventHandler<IAnnotation>? AnnotationDrawn;

    /// <summary>
    /// Asks the session for a fresh tool instance, one per gesture. Null means
    /// no tool is active yet, which keeps the window free of tool state.
    /// </summary>
    public Func<ITool>? ToolFactory { get; set; }

    /// <summary>
    /// Places the window over one monitor. Show() comes first on purpose: the
    /// window has to exist on the target monitor before its size is set, or
    /// Avalonia applies the primary monitor's scaling and the overlay ends up
    /// the wrong size on mixed-DPI setups.
    /// </summary>
    public void PlaceOn(MonitorInfo monitor, Document document)
    {
        _monitorBounds = monitor.Bounds;

        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);
        Show();
        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);

        var scale = RenderScaling;
        Width = monitor.Bounds.Width / scale;
        Height = monitor.Bounds.Height / scale;

        Surface.Attach(document, monitor.Bounds);
    }

    public void ShowSelection(CaptureRect selection) => Surface.ShowSelection(selection);

    public ToolbarView ToolbarControl => Toolbar;

    /// <summary>
    /// Puts the toolbar under the selection, above it when there is no room
    /// below, and inside it when the selection covers the whole screen.
    /// Everything here is in logical pixels - it is layout, not model.
    /// </summary>
    public void PlaceToolbar(CaptureRect selection)
    {
        var scale = RenderScaling;
        var localX = (selection.X - _monitorBounds.X) / scale;
        var localBottom = (selection.Bottom - _monitorBounds.Y) / scale;
        var localTop = (selection.Y - _monitorBounds.Y) / scale;

        Toolbar.IsVisible = true;

        // DesiredSize counts the control's own margin, and the margin is what
        // this method sets. Without the reset every call would stack the
        // previous offset on top of the measured size.
        Toolbar.Margin = default;
        Toolbar.Measure(Size.Infinity);
        var size = Toolbar.DesiredSize;

        var y = localBottom + Gap;

        if (y + size.Height > Height)
        {
            y = localTop - Gap - size.Height;
        }

        if (y < 0)
        {
            y = Math.Max(0, localBottom - Gap - size.Height);
        }

        var x = Math.Clamp(localX, 0, Math.Max(0, Width - size.Width));

        Toolbar.Margin = new Thickness(x, y, 0, 0);
        Toolbar.FadeIn();

        PlaceChip(selection, localX, localTop, toolbarAbove: y < localTop);
    }

    public void HideToolbar()
    {
        Toolbar.FadeOut();
        Toolbar.IsVisible = false;
        Chip.FadeOut();
        Chip.IsVisible = false;
    }

    /// <summary>
    /// Sits above the selection's top-left corner, and moves inside it when
    /// there is no room up there - either the monitor edge is in the way or the
    /// toolbar has already flipped into that spot.
    /// </summary>
    private void PlaceChip(CaptureRect selection, double localX, double localTop, bool toolbarAbove)
    {
        Chip.Update(selection);
        Chip.IsVisible = true;
        Chip.Margin = default;
        Chip.Measure(Size.Infinity);
        var size = Chip.DesiredSize;

        var y = localTop - Gap - size.Height;

        if (y < 0 || toolbarAbove)
        {
            y = localTop + Gap;
        }

        var x = Math.Clamp(localX, 0, Math.Max(0, Width - size.Width));

        Chip.Margin = new Thickness(x, y, 0, 0);
        Chip.FadeIn();
    }

    /// <summary>
    /// Avalonia hands out logical pixels; the model speaks physical ones.
    /// One of the only two places where the two are allowed to meet.
    /// </summary>
    private (int X, int Y) ToVirtualPixels(Point point)
    {
        var scale = RenderScaling;
        return (
            _monitorBounds.X + (int)Math.Round(point.X * scale),
            _monitorBounds.Y + (int)Math.Round(point.Y * scale));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        if (_mode == OverlayMode.Drawing)
        {
            _tool = ToolFactory?.Invoke();
            _tool?.Begin(x, y);
            _dragging = true;
            e.Pointer.Capture(this);
            return;
        }

        _anchorX = x;
        _anchorY = y;
        _dragging = true;

        // Without capture the drag dies the moment the cursor leaves this
        // window - which is exactly what happens on the way to the next monitor.
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging)
        {
            return;
        }

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        if (_mode == OverlayMode.Drawing)
        {
            Surface.ShowPreview(_tool?.Preview(x, y));
            return;
        }

        SelectionChanged?.Invoke(this, CaptureRect.FromPoints(_anchorX, _anchorY, x, y));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        if (_mode == OverlayMode.Drawing)
        {
            var annotation = _tool?.End(x, y);
            _tool = null;
            Surface.ShowPreview(null);

            if (annotation is not null)
            {
                AnnotationDrawn?.Invoke(this, annotation);
            }

            return;
        }

        // The first released selection switches the overlay into drawing:
        // from here the same three events feed the active tool.
        _mode = OverlayMode.Drawing;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            UndoRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
