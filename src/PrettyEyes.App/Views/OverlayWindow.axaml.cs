using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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

    // A Cursor owns a native handle. Building one per mouse move burns handles
    // and hands back the same arrow anyway.
    private static readonly Cursor Cross = new(StandardCursorType.Cross);
    private static readonly Cursor Corner = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor AntiCorner = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor WestEast = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor NorthSouth = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor Move = new(StandardCursorType.SizeAll);

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
    private Size? _toolbarSize;
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

    /// <summary>Ctrl+C or Enter: the same thing the copy button does.</summary>
    public event EventHandler? CopyRequested;

    /// <summary>Ctrl+S: the same thing the save button does.</summary>
    public event EventHandler? SaveRequested;

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

    /// <summary>
    /// Shows and hides the window once so the XAML, the native handle and the
    /// renderer are ready before the first hotkey. Measured cost of doing this
    /// lazily: 69 ms on the first capture, and the user is looking at a screen
    /// that has not frozen yet.
    /// </summary>
    public void WarmUp()
    {
        Width = 1;
        Height = 1;
        Show();
        Position = new PixelPoint(-32000, -32000);
        Hide();
    }

    /// <summary>
    /// Back to the state a fresh window would be in. The windows are pooled, so
    /// anything left over here would show up in the next capture.
    /// </summary>
    public void Reset()
    {
        _selection = CaptureRect.Empty;
        _mode = OverlayMode.Selecting;
        _grip = SelectionGrip.None;
        _dragging = false;
        _tool = null;
        _toolbarSize = null;

        Surface.FrameOpacity = 0;
        Surface.VeilOpacity = 0;
        Surface.ShowPreview(null);

        // Drops the reference to the frozen desktop: 28 MB per capture that
        // would otherwise be held by a hidden window until the next one.
        Surface.Detach();

        HideToolbar();
        HideError();
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

        // Measured once: the panel never changes size, and measuring on every
        // pointer move is a full layout pass for nothing.
        if (_toolbarSize is null)
        {
            Toolbar.Measure(Size.Infinity);
            _toolbarSize = Toolbar.DesiredSize;
        }

        var size = _toolbarSize.Value;

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

        // Moved by transform, not by margin: a margin change relayouts the
        // whole window on every step of a drag.
        Toolbar.RenderTransform = new TranslateTransform(x, y);
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

        // The chip does change width with the number it shows, so it is
        // measured every time - but it is a single text line, and it is moved
        // by transform like the panel.
        Chip.Measure(Size.Infinity);
        var size = Chip.DesiredSize;

        var y = localTop - Gap - size.Height;

        if (y < 0 || toolbarAbove)
        {
            y = localTop + Gap;
        }

        var x = Math.Clamp(localX, 0, Math.Max(0, Width - size.Width));

        Chip.RenderTransform = new TranslateTransform(x, y);
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

        // Anything but the left button leaves the selection alone. Without this
        // a right click on the screen wipes the frame and everything drawn in
        // it, which is what a miss next to a toolbar button looks like.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        HideError();

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        // Double click means the whole monitor: dragging across the screen for
        // the most common capture there is makes no sense.
        if (e.ClickCount == 2 && _mode != OverlayMode.Drawing)
        {
            _dragging = false;
            _grip = SelectionGrip.None;
            _mode = OverlayMode.Adjusting;
            SelectionChanged?.Invoke(this, _monitorBounds);
            SelectionSettled?.Invoke(this, _monitorBounds);
            return;
        }
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

        // A mouse reports far more often than the compositor draws; repeating
        // the same physical pixel only buys another repaint.
        if (x == _lastX && y == _lastY && _dragging)
        {
            return;
        }

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
            Cursor = Cross;
            return;
        }

        Cursor = SelectionGrips.HitTest(_selection, x, y, GripReach) switch
        {
            SelectionGrip.TopLeft or SelectionGrip.BottomRight => Corner,
            SelectionGrip.TopRight or SelectionGrip.BottomLeft => AntiCorner,
            SelectionGrip.Left or SelectionGrip.Right => WestEast,
            SelectionGrip.Top or SelectionGrip.Bottom => NorthSouth,
            SelectionGrip.Inside => Move,
            _ => Cross,
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.Escape:
                Cancelled?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Z when control:
                UndoRequested?.Invoke(this, EventArgs.Empty);
                break;

            // The two things the overlay exists for had no keyboard at all:
            // over a frozen screen everyone reaches for Ctrl+C first.
            case Key.C when control:
            case Key.Enter when _selection.IsEmpty == false:
                CopyRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.S when control:
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
