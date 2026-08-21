using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PrettyEyes.App.Controls;
using PrettyEyes.App.Services;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Pinning;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Tools;
using PrettyEyes.Platform.Windows;
using SkiaSharp;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Views;

/// <summary>
/// One screenshot nailed above every other window.
///
/// It owns its own <see cref="Document"/> with its own cropped image: a shared
/// one would mean a shared undo, a shared Dispose, and every pin holding the
/// whole desktop. What the overlay drew is already baked into those pixels;
/// only what is drawn here is an object.
/// </summary>
public partial class PinnedWindow : Window, IPinned
{
    /// <summary>How much of the window has to stay reachable on a monitor.</summary>
    private const int MustStayVisible = 48;

    private static readonly Cursor Hand = new(StandardCursorType.SizeAll);
    private static readonly Cursor Aim = new(StandardCursorType.Cross);

    private readonly DrawingGesture _gesture;
    private readonly IPointerLocation _pointer = new Win32PointerLocation();

    private Document? _document;
    private ToolStyles _styles = new();
    private ToolKind? _tool;
    private bool _drawingAllowed = true;

    /// <summary>Dragging the window itself, and where the pointer grabbed it.</summary>
    private bool _hauling;
    private PixelPoint _grabbedAt;
    private PixelPoint _wasAt;

    private bool _spaceHeld;

    public PinnedWindow()
    {
        InitializeComponent();

        _gesture = new DrawingGesture(
            point => Surface.ToFramePixels(point),
            Surface.ShowPreview,
            () => _document?.SourceBounds ?? CaptureRect.Empty,
            () => _document,
            MakeTool);

        _gesture.Drawn += (_, annotation) =>
        {
            _document?.Add(annotation);
            Surface.InvalidateVisual();
        };

        _gesture.Moved += (_, moved) =>
        {
            _document?.Move(moved.Annotation, moved.Dx, moved.Dy);
            Surface.InvalidateVisual();
        };

        _gesture.Resized += (_, resized) =>
        {
            _document?.Resize(resized.Annotation, resized.Step);
            Surface.InvalidateVisual();
        };

        Toolbar.ToolPicked += (_, kind) =>
        {
            _tool = kind;
            Toolbar.SetActive(kind);
        };

        Toolbar.UndoClicked += (_, _) =>
        {
            _document?.Undo();
            Surface.InvalidateVisual();
        };

        Toolbar.StyleRequested += (_, kind) => StyleCard.Open(kind, _styles.For(kind));

        StyleCard.StyleChanged += (_, change) =>
        {
            _styles.Set(change.Kind, change.Style);
            Toolbar.ShowStyles(_styles);
        };

        // Told rather than asked: inside the Activated handler IsActive is not
        // true yet, so a toolbar that reads it stays hidden on the very click
        // that was supposed to bring it out.
        Activated += (_, _) => ShowToolbar(active: true);
        Deactivated += (_, _) => ShowToolbar(active: false);
    }

    /// <summary>The chosen emoji, asked for at the moment one is stamped.</summary>
    public Func<SKImage?>? Glyph { get; set; }

    /// <summary>Raised when the window is gone for good.</summary>
    public event EventHandler? Gone;

    /// <inheritdoc/>
    public bool HasOwnAnnotations => _document?.Annotations.Count > 0;

    /// <summary>
    /// Puts the frame on the screen at <paramref name="at"/>.
    /// </summary>
    /// <param name="drawingAllowed">
    /// The "draw on pinned" setting. When it is off the toolbar is not built at
    /// all and the gesture is never offered a press: a switch that leaves the
    /// tools one click away is a switch that does nothing.
    /// </param>
    public void Open(
        Document document,
        CaptureRect at,
        ToolStyles styles,
        ToolVisibility tools,
        bool drawingAllowed)
    {
        _document = document;
        _styles = styles;
        _drawingAllowed = drawingAllowed;

        Surface.Document = document;
        Surface.Zoom = 1;

        Toolbar.CanPin = false;
        Toolbar.ShowTools(tools);
        Toolbar.ShowStyles(styles);

        // No default tool in a pin, on purpose: the left button has to drag the
        // window until somebody says otherwise. See the plan, task 16.
        Toolbar.SetActive(null);

        var size = Surface.Wanted();
        Width = size.Width;
        Height = size.Height;

        Show();
        Position = new PixelPoint(at.X, at.Y);

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        // A pin belongs to the desktop, not to the task switcher: it is always
        // on top, so there is never anything to switch back to.
        WindowSwitcher.Hide(handle);
    }

    /// <summary>Whether a tool is armed and allowed to draw right now.</summary>
    private bool Armed => _drawingAllowed && _tool is not null;

    private ITool? MakeTool() =>
        _tool is { } kind
            ? ToolMaker.Create(
                kind,
                _styles,
                _document?.SourceBounds ?? CaptureRect.Empty,
                () => Glyph?.Invoke())
            : null;

    /// <summary>
    /// The toolbar belongs to the active window and nobody else: a stack of
    /// pins would otherwise be a stack of toolbars.
    ///
    /// Visible is not enough. The card starts fully transparent and lifted, and
    /// only FadeIn puts it where it can be seen - which is how it managed to be
    /// visible and invisible at the same time for two runs.
    /// </summary>
    private void ShowToolbar(bool active)
    {
        var wanted = _drawingAllowed && active;

        Toolbar.IsVisible = wanted;

        if (wanted)
        {
            Toolbar.FadeIn();
        }
        else
        {
            Toolbar.FadeOut();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // The click that wakes the window up does nothing else. Without this
        // the gesture that says "come here" leaves a stray arrow behind, or
        // yanks the window off its place.
        if (!IsActive)
        {
            e.Handled = true;
            return;
        }

        if (StyleCard.IsVisible || EmojiCard.IsVisible)
        {
            StyleCard.Close();
            EmojiCard.Close();
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;

        // The right button always drags the window, whatever is armed. So does
        // the left one while Space is held, and while nothing is armed.
        if (properties.IsRightButtonPressed || _spaceHeld || !Armed)
        {
            Haul(e);
            return;
        }

        if (_drawingAllowed && _gesture.Press(e.GetPosition(Surface), e.KeyModifiers, Armed))
        {
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_hauling)
        {
            Drag();
            return;
        }

        if (!_gesture.Move(e.GetPosition(Surface)))
        {
            Cursor = Armed && !_spaceHeld ? Aim : Hand;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_hauling)
        {
            _hauling = false;
            e.Pointer.Capture(null);
            return;
        }

        if (_gesture.Release(e.GetPosition(Surface)))
        {
            e.Pointer.Capture(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Space:
                _spaceHeld = true;
                break;

            // Same ladder as the overlay: the card first, then the object in
            // mid-air, then the armed tool. What happens after that - closing
            // the window - belongs to task 18.
            case Key.Escape when StyleCard.IsVisible || EmojiCard.IsVisible:
                StyleCard.Close();
                EmojiCard.Close();
                break;

            case Key.Escape when _gesture.CancelCarry():
                Surface.InvalidateVisual();
                break;

            case Key.Escape when _tool is not null:
                _tool = null;
                Toolbar.SetActive(null);
                break;

            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _document?.Undo();
                Surface.InvalidateVisual();
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
        }
    }

    private void Haul(PointerPressedEventArgs e)
    {
        _hauling = true;
        _wasAt = Position;
        _grabbedAt = Pointer();
        e.Pointer.Capture(this);
        Cursor = Hand;
    }

    private void Drag()
    {
        var now = Pointer();
        var wanted = new PixelPoint(
            _wasAt.X + (now.X - _grabbedAt.X),
            _wasAt.Y + (now.Y - _grabbedAt.Y));

        Position = Reachable(wanted);
    }

    /// <summary>
    /// The pointer in desktop pixels. Taken from the platform rather than from
    /// the event: a window being dragged moves under the pointer, so a position
    /// measured against the window chases its own tail.
    /// </summary>
    private PixelPoint Pointer()
    {
        var (x, y) = _pointer.Current;

        return new PixelPoint(x, y);
    }

    /// <summary>
    /// Keeps a corner of the window on some monitor. A pin dragged fully off
    /// the desktop cannot be dragged back, and its contents exist nowhere else.
    /// </summary>
    private PixelPoint Reachable(PixelPoint wanted)
    {
        var screens = Screens.All;

        if (screens.Count == 0)
        {
            return wanted;
        }

        var width = (int)Math.Round(Bounds.Width * RenderScaling);
        var height = (int)Math.Round(Bounds.Height * RenderScaling);

        var left = screens.Min(screen => screen.Bounds.X);
        var top = screens.Min(screen => screen.Bounds.Y);
        var right = screens.Max(screen => screen.Bounds.X + screen.Bounds.Width);
        var bottom = screens.Max(screen => screen.Bounds.Y + screen.Bounds.Height);

        return new PixelPoint(
            Math.Clamp(wanted.X, left - width + MustStayVisible, right - MustStayVisible),
            Math.Clamp(wanted.Y, top - height + MustStayVisible, bottom - MustStayVisible));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _document?.Dispose();
        _document = null;
        Surface.Document = null;

        Gone?.Invoke(this, EventArgs.Empty);
    }
}
