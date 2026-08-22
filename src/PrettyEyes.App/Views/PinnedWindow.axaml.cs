using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PrettyEyes.App.Controls;
using PrettyEyes.App.Services;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
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

    /// <summary>
    /// Zoom limits. Below a tenth a screenshot is a stamp, above eight times it
    /// is wallpaper.
    /// </summary>
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8;

    /// <summary>
    /// Where tools still make sense. At eight times a three-pixel stroke is a
    /// sausage across the screen; at a tenth nothing can be aimed at. Outside
    /// this the pin behaves as though no tool were armed at all - otherwise the
    /// left button would simply stop doing anything.
    /// </summary>
    private const double MinDrawZoom = 0.25;
    private const double MaxDrawZoom = 4;

    /// <summary>One wheel notch.</summary>
    private const double ZoomStep = 1.1;

    /// <summary>
    /// Below this a pin is an invisible trap for clicks: it still swallows
    /// them, and there is nothing on screen to say why.
    /// </summary>
    private const double MinOpacity = 0.2;

    private const double OpacityStep = 0.05;

    /// <summary>Where transparency currently stands, 1 being solid.</summary>
    private double _seeThrough = 1;

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

    /// <summary>The close was asked for and is waiting for an answer.</summary>
    private bool _asking;

    /// <summary>What a yes means. Closing this one, or closing all of them.</summary>
    private Action? _onYes;

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

        Toolbar.CopyClicked += (_, _) => CopyRequested?.Invoke(this, EventArgs.Empty);
        Toolbar.SaveClicked += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
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

        CloseButton.Click += (_, _) => AskThenClose();
        KeepButton.Click += (_, _) => Ask(false);
        DiscardButton.Click += (_, _) => _onYes?.Invoke();
    }

    /// <summary>The chosen emoji, asked for at the moment one is stamped.</summary>
    public Func<SKImage?>? Glyph { get; set; }

    /// <summary>Raised when the window is gone for good.</summary>
    public event EventHandler? Gone;

    /// <summary>The frame is wanted in the clipboard.</summary>
    public event EventHandler? CopyRequested;

    /// <summary>The frame is wanted in a file, through the dialog.</summary>
    public event EventHandler? SaveRequested;

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
        bool drawingAllowed,
        double opacity)
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

        SeeThrough(opacity);
    }

    /// <summary>
    /// The frame as it would go into a file: in frame pixels, at whatever the
    /// export style says. Zoom and transparency are how the window is shown,
    /// not what it holds, so neither is baked in.
    /// </summary>
    public SKImage? Snapshot(ExportStyle style) =>
        _document is null ? null : DocumentRenderer.Render(_document, style);

    /// <summary>Whether this window shows up in captures of the screen.</summary>
    public void HideFromCapture(bool hidden) =>
        WindowCapture.Exclude(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, hidden);

    /// <summary>
    /// Whether a tool is armed and allowed to draw right now. Zoom counts: a
    /// tool the zoom has switched off is the same as no tool, so the left
    /// button goes back to dragging the window.
    /// </summary>
    private bool Armed =>
        _drawingAllowed
        && _tool is not null
        && Surface.Zoom >= MinDrawZoom
        && Surface.Zoom <= MaxDrawZoom;

    /// <summary>
    /// The wheel. Plain is zoom, Ctrl is transparency, Alt is the size of the
    /// object under the pointer.
    ///
    /// Ctrl means transparency here and only here: in the overlay it kept its
    /// old meaning, and the clash it would have caused exists only inside a
    /// pinned window.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var up = e.Delta.Y > 0;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            e.Handled = _gesture.Wheel(e.GetPosition(Surface), e.Delta.Y);
            Surface.InvalidateVisual();

            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SeeThrough(Math.Clamp(_seeThrough + (up ? OpacityStep : -OpacityStep), MinOpacity, 1));
            e.Handled = true;

            return;
        }

        ZoomBy(up ? ZoomStep : 1 / ZoomStep);
        e.Handled = true;
    }

    /// <summary>
    /// Asks the compositor for the alpha rather than setting Avalonia's own
    /// Opacity: on Win32 that one leaves the window unlayered and changes
    /// nothing at all, which is exactly how it behaved on the first live run.
    /// </summary>
    private void SeeThrough(double opacity)
    {
        _seeThrough = opacity;

        WindowTransparency.Set(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, opacity);
    }

    /// <summary>
    /// Grows or shrinks the window around its top-left corner. Around the
    /// corner rather than the centre on purpose: a pin is put somewhere for a
    /// reason, and a window that walks off while being zoomed loses that place.
    /// </summary>
    private void ZoomBy(double factor)
    {
        Surface.Zoom = Math.Clamp(Surface.Zoom * factor, MinZoom, MaxZoom);

        var size = Surface.Wanted();
        Width = size.Width;
        Height = size.Height;
    }

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
        CloseButton.IsVisible = active;

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

        // A question is up. Everything else waits for the answer, including
        // dragging the window out from under it.
        if (_asking)
        {
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

            case Key.Escape when _asking:
                Ask(false);
                break;

            case Key.Escape when _tool is not null:
                _tool = null;
                Toolbar.SetActive(null);
                break;

            // And the ladder stops there. Escape does not close a pin at all.
            //
            // It used to, as the last rung. In use that turned out to be a
            // trap: the key that puts the overlay away is pressed by reflex a
            // moment after pinning, and it took the pin with it. Losing a
            // window to a habit is worse than reaching for its cross.

            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                CopyRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                SaveRequested?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Closes, or asks first. Asking, because what was drawn here exists
    /// nowhere else: it was never a file and never went to the clipboard.
    /// </summary>
    public void AskThenClose()
    {
        if (!HasOwnAnnotations)
        {
            Close();
            return;
        }

        Question("Закрыть? Нарисованное здесь пропадёт", Close);
    }

    /// <summary>
    /// Asks something in this window and does <paramref name="yes"/> if the
    /// answer is yes. Used for "close all of them" as well, which is why the
    /// action is a parameter: the question is one, the windows are several.
    /// </summary>
    public void Question(string text, Action yes)
    {
        ConfirmText.Text = text;
        _onYes = yes;
        Ask(true);

        // The question is only readable if the window is in front of the rest
        // of the stack; it was asked from the tray, not from here.
        Activate();
    }

    private void Ask(bool asking)
    {
        _asking = asking;
        ConfirmBar.IsVisible = asking;
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
