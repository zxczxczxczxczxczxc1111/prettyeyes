using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using PrettyEyes.Platform.Windows;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Views;

public partial class OverlayWindow : Window
{
    private const double Gap = 8;

    /// <summary>How far from an edge a pointer still counts as grabbing it.</summary>
    private const int GripReach = 8;

    private CaptureRect _monitorBounds;
    private CaptureRect _frameBounds;
    private CaptureRect _selection;
    private int _anchorX;
    private int _anchorY;
    private int _lastX;
    private int _lastY;
    private bool _dragging;
    private SelectionGrip _grip = SelectionGrip.None;
    private OverlayMode _mode = OverlayMode.Selecting;
    private ITool? _tool;

    public OverlayWindow() => InitializeComponent();

    public event EventHandler<CaptureRect>? SelectionChanged;

    public event EventHandler? Cancelled;

    public event EventHandler? UndoRequested;

    /// <summary>
    /// The selection gesture finished. The toolbar and the size chip appear on
    /// this, not on every pointer move.
    /// </summary>
    public event EventHandler<CaptureRect>? SelectionSettled;

    /// <summary>Raised once a tool gesture produced a shape.</summary>
    public event EventHandler<IAnnotation>? AnnotationDrawn;

    /// <summary>
    /// Asks the session for a fresh tool instance, one per gesture. Null means
    /// no tool is active yet, which keeps the window free of tool state.
    /// </summary>
    public Func<ITool?>? ToolFactory { get; set; }

    /// <summary>
    /// Places the window over one monitor. Show() comes first on purpose: the
    /// window has to exist on the target monitor before its size is set, or
    /// Avalonia applies the primary monitor's scaling and the overlay ends up
    /// the wrong size on mixed-DPI setups.
    /// </summary>
    public void PlaceOn(MonitorInfo monitor, Document document)
    {
        _monitorBounds = monitor.Bounds;
        _frameBounds = document.SourceBounds;

        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);
        Show();
        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);

        // The overlay lives and dies inside one capture; it has no place in
        // Alt+Tab, and being topmost it never needs to be switched back to.
        WindowSwitcher.Hide(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

        var scale = RenderScaling;
        Width = monitor.Bounds.Width / scale;
        Height = monitor.Bounds.Height / scale;

        Surface.Attach(document, monitor.Bounds);

        // Fades the veil in; the value itself is animated by the canvas.
        Surface.VeilOpacity = 1;
    }

    public void ShowSelection(CaptureRect selection)
    {
        _selection = selection;
        Surface.ShowSelection(selection);
    }

    /// <summary>
    /// Called by the session whenever the picked tool changes. Without a tool
    /// the pointer goes back to editing the selection.
    /// </summary>
    public void SetToolActive(bool active)
    {
        if (_selection.IsEmpty)
        {
            return;
        }

        _mode = active ? OverlayMode.Drawing : OverlayMode.Adjusting;
    }

    /// <summary>Back to picking a region, with the frame gone.</summary>
    public void ResetSelection()
    {
        _selection = CaptureRect.Empty;
        _mode = OverlayMode.Selecting;
        _grip = SelectionGrip.None;
        Surface.FrameOpacity = 0;
        Surface.ShowSelection(CaptureRect.Empty);
    }

    /// <summary>
    /// A failed copy or save must not throw the capture away, so the message
    /// lands inside the overlay and goes away on the next click.
    /// </summary>
    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBanner.IsVisible = true;
    }

    public void HideError() => ErrorBanner.IsVisible = false;

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

        // Anchored to the right edge of the selection: a right-handed drag ends
        // there, so that is where the hand already is.
        var localRight = (selection.Right - _monitorBounds.X) / scale;
        var x = Math.Clamp(localRight - size.Width, 0, Math.Max(0, Width - size.Width));

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
        // The renderer crops the selection to the captured frame, so the chip
        // has to show the cropped size or it promises pixels that never arrive.
        var visible = selection.Intersect(_frameBounds);
        Chip.Update(visible.IsEmpty ? selection : visible);
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

        HideError();

        var (x, y) = ToVirtualPixels(e.GetPosition(this));
        _lastX = x;
        _lastY = y;
        _dragging = true;

        // Without capture the drag dies the moment the cursor leaves this
        // window - which is exactly what happens on the way to the next monitor.
        e.Pointer.Capture(this);

        if (_mode == OverlayMode.Drawing)
        {
            var (toolX, toolY) = _selection.ClampPoint(x, y);
            _tool = ToolFactory?.Invoke();
            _tool?.Begin(toolX, toolY);
            return;
        }

        _grip = SelectionGrips.HitTest(_selection, x, y, GripReach);

        if (_grip == SelectionGrip.None)
        {
            // Nowhere near the current selection: start a fresh one.
            _anchorX = x;
            _anchorY = y;
            _mode = OverlayMode.Selecting;
            SelectionChanged?.Invoke(this, CaptureRect.FromPoints(x, y, x, y));
        }
        else
        {
            _mode = OverlayMode.Adjusting;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        if (!_dragging)
        {
            UpdateCursor(x, y);
            return;
        }

        switch (_mode)
        {
            case OverlayMode.Drawing:
                var (toolX, toolY) = _selection.ClampPoint(x, y);
                Surface.ShowPreview(_tool?.Preview(toolX, toolY));
                break;

            case OverlayMode.Selecting:
                SelectionChanged?.Invoke(this, CaptureRect.FromPoints(_anchorX, _anchorY, x, y));
                break;

            case OverlayMode.Adjusting:
                var moved = SelectionGrips.Apply(_selection, _grip, x - _lastX, y - _lastY, _frameBounds);
                SelectionChanged?.Invoke(this, moved);
                break;
        }

        _lastX = x;
        _lastY = y;
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
            var (toolX, toolY) = _selection.ClampPoint(x, y);
            var annotation = _tool?.End(toolX, toolY);
            _tool = null;
            Surface.ShowPreview(null);

            if (annotation is not null)
            {
                AnnotationDrawn?.Invoke(this, annotation);
            }

            return;
        }

        _grip = SelectionGrip.None;
        _mode = OverlayMode.Adjusting;

        if (_selection.IsEmpty)
        {
            return;
        }

        // The gesture is over: the toolbar belongs to this moment, not to every
        // pointer move on the way here.
        SelectionSettled?.Invoke(this, _selection);
    }

    /// <summary>The cursor says what the next press will do.</summary>
    private void UpdateCursor(int x, int y)
    {
        if (_mode == OverlayMode.Drawing)
        {
            Cursor = new Cursor(StandardCursorType.Cross);
            return;
        }

        Cursor = SelectionGrips.HitTest(_selection, x, y, GripReach) switch
        {
            SelectionGrip.TopLeft or SelectionGrip.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            SelectionGrip.TopRight or SelectionGrip.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            SelectionGrip.Left or SelectionGrip.Right => new Cursor(StandardCursorType.SizeWestEast),
            SelectionGrip.Top or SelectionGrip.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
            SelectionGrip.Inside => new Cursor(StandardCursorType.SizeAll),
            _ => new Cursor(StandardCursorType.Cross),
        };
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
