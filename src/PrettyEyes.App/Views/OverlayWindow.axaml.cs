using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using PrettyEyes.Platform.Windows;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Views;

public partial class OverlayWindow : Window
{
    private const double Gap = 8;

    /// <summary>How far from an edge a pointer still counts as grabbing it.</summary>
    private const int GripReach = 8;

    // Mirrors the numbers the canvas draws with: the label has to line up with
    // a magnifier it does not draw itself.
    private const int MagnifierSize = 132;
    private const int MagnifierGap = 24;

    /// <summary>
    /// Smaller than this in either direction and a text drag was a click. A
    /// hand does not hold still on a mouse button, and every click would
    /// otherwise produce a label two pixels wide.
    /// </summary>
    private const int TextBoxReach = 12;

    /// <summary>Arrow keys nudge by one pixel, with Shift by ten.</summary>
    private const int NudgeStep = 1;
    private const int NudgeStepFast = 10;

    // A Cursor owns a native handle. Building one per mouse move burns handles
    // and hands back the same arrow anyway.

    private static readonly Cursor Corner = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor AntiCorner = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor WestEast = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor NorthSouth = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor Move = new(StandardCursorType.SizeAll);
    private static readonly Cursor Caret = new(StandardCursorType.Ibeam);

    private CaptureRect _monitorBounds;

    /// <summary>The monitor minus the taskbar. Panels stay inside it.</summary>
    private CaptureRect _monitorUsable;

    private CaptureRect _frameBounds;
    private CaptureRect _selection;
    private int _anchorX;
    private int _anchorY;
    private int _lastX;
    private int _lastY;
    private bool _dragging;

    /// <summary>The document, for finding what is under the pointer.</summary>
    private Document? _document;

    private CursorStyle _cursorStyle = CursorStyle.Cross;

    /// <summary>Which ink the cursor is drawn with at the moment.</summary>
    private bool _darkInk;

    /// <summary>
    /// How far around the pointer the picture is measured to choose the ink.
    /// Roughly what the cursor itself covers.
    /// </summary>
    private const int CursorReach = 12;

    private SelectionGrip _grip = SelectionGrip.None;

    /// <summary>The selection as it stood when the current click began.</summary>
    private CaptureRect _beforeGesture;

    /// <summary>What a second double click puts back. Null once it has.</summary>
    private CaptureRect? _restore;

    /// <summary>Dragging over a label to pick characters, not to draw.</summary>
    private bool _pickingText;

    /// <summary>Dragging out the box a new label will wrap inside.</summary>
    private bool _placingText;
    private int _textAnchorX;
    private int _textAnchorY;
    private OverlayMode _mode = OverlayMode.Selecting;

    /// <summary>
    /// Drawing and carrying, shared with the pinned window. Called from the
    /// handlers below rather than subscribed: this window has a card to close,
    /// a double click and selection grips to get through first.
    /// </summary>
    private readonly DrawingGesture _gesture;
    private Size? _toolbarSize;
    private bool _magnifierWanted = true;
    private ExportStyle? _export;

    public OverlayWindow()
    {
        InitializeComponent();

        _gesture = new DrawingGesture(
            ToVirtualPixels,
            Surface.ShowPreview,
            () => _selection,
            () => _document,
            () => ToolFactory?.Invoke());

        _gesture.Drawn += (_, annotation) => AnnotationDrawn?.Invoke(this, annotation);
        _gesture.Moved += (_, moved) => AnnotationMoved?.Invoke(this, moved);
        _gesture.Resized += (_, resized) => AnnotationResized?.Invoke(this, resized);
    }

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

    /// <summary>A stamped glyph was carried somewhere else and let go.</summary>
    public event EventHandler<(IMovable Annotation, int Dx, int Dy)>? AnnotationMoved;

    /// <summary>A stamped glyph should grow or shrink by one step.</summary>
    public event EventHandler<(IMovable Annotation, int Steps)>? AnnotationResized;

    /// <summary>Ctrl+C or Enter: the same thing the copy button does.</summary>
    public event EventHandler? CopyRequested;

    /// <summary>Ctrl+S: save to a file, through the dialog, like the button.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>A hex colour that wants to be in the clipboard as text.</summary>
    public event EventHandler<string>? ColourCopyRequested;

    /// <summary>Escape while a tool is held: put the pointer back on the frame.</summary>
    public event EventHandler? ToolCleared;

    /// <summary>
    /// Asks the session for a fresh tool instance, one per gesture. Null means
    /// no tool is active yet, which keeps the window free of tool state.
    /// </summary>
    public Func<ITool?>? ToolFactory { get; set; }

    /// <summary>
    /// Whether a caret is up anywhere in this capture. Asked before any key is
    /// looked at, and deliberately not "in this window": clicking the toolbar
    /// moves the keyboard to the window the toolbar is in, which on two
    /// monitors is not the window the caret is in.
    /// </summary>
    public Func<bool>? TypingActive { get; set; }

    /// <summary>
    /// The armed tool is the text one. It places a caret rather than drawing,
    /// so the pointer path skips ToolFactory entirely.
    /// </summary>
    public bool TextToolArmed { get; set; }

    /// <summary>A key arrived while a caret was up. The session reads it.</summary>
    public event EventHandler<KeyEventArgs>? TextKeyPressed;

    /// <summary>Characters, already composed by the input method.</summary>
    public event EventHandler<string>? TextEntered;

    /// <summary>
    /// A press while a caret is up. True means the session took it - the caret
    /// moved, a selection started - and the window keeps the gesture. False
    /// means the press landed outside and has already committed the label.
    /// </summary>
    public Func<int, int, int, bool>? TextPointerPressed { get; set; }

    /// <summary>
    /// A place for a new label. Null width is a click, where the label grows
    /// with the text; a number is a dragged box the text wraps inside.
    /// </summary>
    public event EventHandler<(int X, int Y, int? MaxWidth)>? TextPlaced;

    /// <summary>The pointer is picking characters with the button held.</summary>
    public event EventHandler<(int X, int Y)>? TextSelectionDragged;

    /// <summary>
    /// The box for a new label, as it stands mid-drag. The session draws it,
    /// because the colour it is drawn in is the text style and that lives there.
    /// </summary>
    public event EventHandler<CaptureRect>? TextBoxDragged;

    /// <summary>Double click on a finished label: back into it.</summary>
    public event EventHandler<TextAnnotation>? TextEditRequested;

    /// <summary>
    /// The label being typed, drawn above the document like any other preview.
    /// Nothing here reaches the screenshot until the session commits it.
    /// </summary>
    public void ShowTextPreview(IAnnotation? preview) => Surface.ShowPreview(preview);

    /// <summary>
    /// Whether this is the window the caret is in. Switching it off drops back
    /// to editing the selection; the session says SetToolActive right after,
    /// because whether a tool is armed is its business and not this window's.
    /// </summary>
    public void SetTyping(bool typing)
    {
        _mode = typing
            ? OverlayMode.Typing
            : _selection.IsEmpty ? OverlayMode.Selecting : OverlayMode.Adjusting;

        if (typing)
        {
            HideMagnifier();
        }
        else
        {
            Surface.ShowPreview(null);
        }
    }

    /// <summary>
    /// Places the window over one monitor. Show() comes first on purpose: the
    /// window has to exist on the target monitor before its size is set, or
    /// Avalonia applies the primary monitor's scaling and the overlay ends up
    /// the wrong size on mixed-DPI setups.
    /// </summary>
    public void PlaceOn(MonitorInfo monitor, Document document)
    {
        _monitorBounds = monitor.Bounds;
        _monitorUsable = monitor.Usable;

        // Windows are pooled, so nothing here is fresh unless it is made
        // fresh: a selection remembered from the previous capture would come
        // back on the first double click of this one.
        _beforeGesture = CaptureRect.Empty;
        _restore = null;
        _frameBounds = document.SourceBounds;
        _document = document;

        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);

        // Filled before it is shown. Between becoming visible and drawing its
        // first frame - measured at 44 ms for a canvas this size - a window
        // shows nothing but its own background, and the window is transparent
        // exactly so that "nothing" is the real desktop rather than a black
        // flash or the frame left over from the previous capture.
        Surface.Attach(document, monitor.Bounds, monitor.Usable);
        Resize(monitor);

        Show();
        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        // The overlay lives and dies inside one capture; it has no place in
        // Alt+Tab, and being topmost it never needs to be switched back to.
        WindowSwitcher.Hide(handle);

        // Again: a window moved to a monitor with different scaling only learns
        // its new scale once it is on it.
        Resize(monitor);


        // Started a beat later, and that beat is the whole point. Setting a
        // transitioned property does two things in this order: the property
        // takes the new value, and the transition then walks it there from the
        // old one. A frame drawn in between - and the first frame after Show
        // lands exactly there - is drawn fully dimmed, after which the
        // transition puts it back to nothing and fades it in properly. That is
        // the flash: dark, undimmed, then dark again.
        Dispatcher.UIThread.Post(() => Surface.VeilOpacity = 1, DispatcherPriority.Background);
    }

    /// <summary>
    /// One pixel short of the monitor, on purpose.
    ///
    /// A window that covers a monitor exactly is a game or a video player as
    /// far as Windows is concerned, and it switches Do Not Disturb on for the
    /// duration: a bell appears in the notification area, everything there
    /// shifts along to make room, and shifts back when the overlay closes. That
    /// is a taskbar twitching on every single screenshot. Asking the shell
    /// nicely through ITaskbarList2 changed nothing - measured - and one pixel
    /// changes everything.
    ///
    /// What it costs: the bottom row of the screen is not dimmed and cannot be
    /// clicked. A selection dragged downwards still reaches it, and the
    /// whole-monitor shot is unaffected - both work from the captured frame
    /// rather than from this window.
    /// </summary>
    private void Resize(MonitorInfo monitor)
    {
        var scale = RenderScaling;

        Width = monitor.Bounds.Width / scale;
        Height = (monitor.Bounds.Height - 1) / scale;
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

        if (active)
        {
            HideMagnifier();
        }
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
    /// This window has the pointer. On more than one monitor the other overlays
    /// stop getting pointer moves the moment the cursor crosses the boundary,
    /// and without this they keep drawing the last place they saw it.
    /// </summary>
    public event EventHandler? PointerSeen;

    /// <summary>Takes the magnifier away because another overlay has it now.</summary>
    public void DropMagnifier() => HideMagnifier();

    /// <summary>
    /// Starts the overlay disappearing. The veil and the frame walk down through
    /// their own transitions; the window is hidden by the pool once they have.
    /// </summary>
    public void FadeOut()
    {
        Surface.VeilOpacity = 0;
        Surface.FrameOpacity = 0;
        Surface.ShowPreview(null);
        HideMagnifier();
        HideToolbar();
        StyleCard.Close();
        EmojiCard.Close();
        Chip.IsVisible = false;
    }

    /// <summary>Off means the magnifier never shows, whatever the pointer does.</summary>
    public void SetMagnifierEnabled(bool enabled)
    {
        _magnifierWanted = enabled;

        if (!enabled)
        {
            HideMagnifier();
        }
    }

    /// <summary>The shape the pointer takes while aiming.</summary>
    public void SetCursorStyle(CursorStyle style) => _cursorStyle = style;

    /// <summary>The pixel grid inside the magnifier, on or off.</summary>
    public void SetMagnifierGrid(bool grid) => Surface.MagnifierGrid = grid;

    /// <summary>The export style, so the size chip can tell the truth.</summary>
    public void SetExportStyle(ExportStyle style) => _export = style;

    /// <summary>
    /// Aims the magnifier, or takes it away. It has no business being up while
    /// a tool is drawing, while the pointer is over the toolbar, or when the
    /// user switched it off.
    /// </summary>
    private void UpdateMagnifier(int x, int y)
    {
        // Raised even when this window is about to hide its own magnifier: the
        // pointer being over this toolbar is still news to the other monitor,
        // which would otherwise keep the magnifier it drew a moment ago.
        PointerSeen?.Invoke(this, EventArgs.Empty);

        if (!_magnifierWanted || _mode is OverlayMode.Drawing or OverlayMode.Typing || OverToolbar(x, y))
        {
            HideMagnifier();
            return;
        }

        Surface.MagnifierAt = new PixelPoint(x, y);

        var box = MagnifierPlacement.Choose(x, y, _monitorBounds, MagnifierSize, MagnifierGap);
        var scale = RenderScaling;

        Loupe.IsVisible = true;
        Loupe.Measure(Size.Infinity);

        // Centred under the magnifier, and never off the monitor.
        var width = Loupe.DesiredSize.Width;
        var left = (((box.X + (box.Width / 2.0)) - _monitorBounds.X) / scale) - (width / 2);
        var top = ((box.Bottom - _monitorBounds.Y) / scale) + 6;

        if (top + Loupe.DesiredSize.Height > Height)
        {
            top = ((box.Y - _monitorBounds.Y) / scale) - 6 - Loupe.DesiredSize.Height;
        }

        Loupe.RenderTransform = new TranslateTransform(
            Math.Clamp(left, 0, Math.Max(0, Width - width)),
            Math.Max(0, top));

        if (_dragging && _mode != OverlayMode.Drawing && !_selection.IsEmpty)
        {
            Loupe.ShowSize(_selection);
        }
        else
        {
            Loupe.ShowPixel(x, y, Surface.ColorAt(x, y));
        }

        Loupe.Show();
    }

    private void HideMagnifier()
    {
        Surface.MagnifierAt = null;
        Loupe.Hide();
    }

    /// <summary>
    /// Whether the pointer is over the toolbar or the chip. The magnifier drops
    /// out below and to the right of the cursor, which is exactly where the
    /// toolbar sits after a right-handed drag.
    /// </summary>
    private bool OverToolbar(int x, int y)
    {
        var scale = RenderScaling;
        var localX = (x - _monitorBounds.X) / scale;
        var localY = (y - _monitorBounds.Y) / scale;

        return Covers(Toolbar, localX, localY) || Covers(Chip, localX, localY);
    }

    private static bool Covers(Control control, double x, double y)
    {
        if (!control.IsVisible || control.RenderTransform is not TranslateTransform at)
        {
            return false;
        }

        var size = control.DesiredSize;

        // A little slack: the magnifier appearing right at the panel's edge
        // reads as a glitch either way.
        return x >= at.X - MagnifierGap
            && y >= at.Y - MagnifierGap
            && x <= at.X + size.Width + MagnifierGap
            && y <= at.Y + size.Height + MagnifierGap;
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
        _pickingText = false;
        _placingText = false;
        _gesture.Reset();
        _toolbarSize = null;

        // Snapped rather than animated: see SnapOpacities.
        Surface.SnapOpacities(veil: 0, frame: 0);
        Surface.ShowPreview(null);
        HideMagnifier();
        StyleCard.Close();
        EmojiCard.Close();

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

    public ToolStylePopup StyleCardControl => StyleCard;

    public EmojiPickerView EmojiCardControl => EmojiCard;

    /// <summary>The glyph grid, placed like the style card.</summary>
    public void ShowEmojiCard()
    {
        EmojiCard.Open();
        Place(EmojiCard);
    }

    public void HideEmojiCard() => EmojiCard.Close();

    /// <summary>
    /// Opens the style card under the tool button, or above it when the toolbar
    /// itself has already flipped above the selection.
    /// </summary>
    public void ShowStyleCard(ToolKind kind, ToolStyle style)
    {
        StyleCard.Open(kind, style);
        Place(StyleCard);
    }

    /// <summary>
    /// Under the toolbar, or above it when the toolbar itself has already
    /// flipped above the selection and there is no room below.
    /// </summary>
    private void Place(Control card)
    {
        if (Toolbar.RenderTransform is not TranslateTransform panel)
        {
            return;
        }

        card.Measure(Size.Infinity);

        var size = card.DesiredSize;

        // The toolbar stands in for the selection here: the card belongs to the
        // panel, not to the frame.
        var y = PanelPlacement.Vertical(
            panel.Y,
            panel.Y + Toolbar.DesiredSize.Height,
            size.Height,
            Gap,
            LimitTop,
            LimitBottom);

        card.RenderTransform = new TranslateTransform(
            Math.Clamp(panel.X, 0, Math.Max(0, Width - size.Width)),
            y);
    }

    public void HideStyleCard() => StyleCard.Close();

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

        var y = PanelPlacement.Vertical(localTop, localBottom, size.Height, Gap, LimitTop, LimitBottom);

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

    /// <summary>
    /// The rows a panel is allowed to use, in this window's logical pixels.
    /// Bounded by the taskbar rather than by the screen: the overlay stops one
    /// pixel short of the monitor so that Windows does not call it a game, and
    /// the price of that is a taskbar drawn on top of us. A panel put under it
    /// is simply gone - which is what a whole-monitor selection used to do to
    /// the toolbar.
    /// </summary>
    private double LimitTop => Math.Max(0, (Usable.Y - _monitorBounds.Y) / RenderScaling);

    private double LimitBottom =>
        Math.Min(Height, (Usable.Bottom - _monitorBounds.Y) / RenderScaling);

    private CaptureRect Usable => _monitorUsable.IsEmpty ? _monitorBounds : _monitorUsable;

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
        Chip.Update(visible.IsEmpty ? selection : visible, _export);
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

        // A click anywhere outside the card closes it, and that click does
        // nothing else: it was aimed at the card, not at the screen.
        if (StyleCard.IsVisible || EmojiCard.IsVisible)
        {
            StyleCard.Close();
            EmojiCard.Close();
            return;
        }

        // Anything but the left button leaves the selection alone. Without this
        // a right click on the screen wipes the frame and everything drawn in
        // it, which is what a miss next to a toolbar button looks like.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        HideError();

        var (x, y) = ToVirtualPixels(e.GetPosition(this));

        // A caret is up somewhere. Either this press belongs to it, or it is
        // the press that finishes the label - and in both cases it is the only
        // thing this press does.
        if (TypingActive?.Invoke() == true)
        {
            if (TextPointerPressed?.Invoke(x, y, e.ClickCount) == true)
            {
                _dragging = true;
                _pickingText = true;
                e.Pointer.Capture(this);
            }

            return;
        }

        // Back into a finished label. A single click, not a double one: the
        // first click of a double would already have placed a fresh caret on
        // top of the label, and the second would land in that instead. The
        // double click then means what it means everywhere else - select all -
        // because by the time it arrives a caret is up and the branch above
        // takes it.
        //
        // Ctrl is excluded on purpose: that is the carry gesture, and it has to
        // keep working on labels like it does on every other annotation.
        if (TextToolArmed
            && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && _document?.MovableAt(x, y) is TextAnnotation label)
        {
            TextEditRequested?.Invoke(this, label);
            return;
        }

        // What the selection was before this click started changing it. The
        // first click of a double click already wipes it, so by the time the
        // second one arrives the only witness left is this field.
        if (e.ClickCount == 1)
        {
            _beforeGesture = _selection;
        }

        // Double click means the whole monitor: dragging across the screen for
        // the most common capture there is makes no sense. Doing it again puts
        // back whatever was selected before - a whole screen where a small
        // frame used to be is the kind of mistake worth one click to undo.
        if (e.ClickCount == 2 && _mode != OverlayMode.Drawing)
        {
            _dragging = false;
            _grip = SelectionGrip.None;

            CaptureRect selection;

            if (_beforeGesture == _monitorBounds && _restore is not null)
            {
                selection = _restore.Value;
                _restore = null;
            }
            else
            {
                _restore = _beforeGesture;
                selection = _monitorBounds;
            }

            // Nothing selected is a legal thing to go back to: the first double
            // click of a capture replaces exactly that.
            _mode = selection.IsEmpty ? OverlayMode.Selecting : OverlayMode.Adjusting;

            SelectionChanged?.Invoke(this, selection);
            SelectionSettled?.Invoke(this, selection);
            return;
        }
        _lastX = x;
        _lastY = y;
        _dragging = true;

        // Without capture the drag dies the moment the cursor leaves this
        // window - which is exactly what happens on the way to the next monitor.
        e.Pointer.Capture(this);

        // The text tool has no gesture to build a shape out of: the press and
        // the release together only say where the caret goes. It is this
        // window's business alone, so it is settled before the shared gesture
        // is offered the press.
        if (_mode == OverlayMode.Drawing && TextToolArmed)
        {
            (_textAnchorX, _textAnchorY) = _selection.ClampPoint(x, y);
            _placingText = true;

            return;
        }

        if (_gesture.Press(e.GetPosition(this), e.KeyModifiers, _mode == OverlayMode.Drawing))
        {
            if (_gesture.Carrying)
            {
                HideMagnifier();
            }

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
            UpdateCursor(x, y, e.KeyModifiers);
            UpdateMagnifier(x, y);
            return;
        }

        if (_gesture.Carrying)
        {
            _gesture.Move(e.GetPosition(this));
            _lastX = x;
            _lastY = y;
            return;
        }

        if (_pickingText)
        {
            TextSelectionDragged?.Invoke(this, (x, y));
            _lastX = x;
            _lastY = y;

            return;
        }

        UpdateMagnifier(x, y);

        switch (_mode)
        {
            case OverlayMode.Drawing:
                var (toolX, toolY) = _selection.ClampPoint(x, y);

                if (_placingText)
                {
                    TextBoxDragged?.Invoke(
                        this,
                        CaptureRect.FromPoints(_textAnchorX, _textAnchorY, toolX, toolY));

                    break;
                }

                _gesture.Move(e.GetPosition(this));
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

    /// <summary>
    /// Ctrl and the wheel over a stamped glyph makes it bigger or smaller.
    /// The same modifier that picks a glyph up, so there is one thing to
    /// remember rather than two.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Alt is a synonym for Ctrl here rather than a replacement: the Ctrl
        // gesture shipped, and breaking it for everybody - including people who
        // never pin anything - to resolve a clash that only exists inside a
        // pinned window would be a poor trade.
        if (Grabbing(e.KeyModifiers))
        {
            e.Handled = _gesture.Wheel(e.GetPosition(this), e.Delta.Y);
        }
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

        if (_pickingText)
        {
            _pickingText = false;
            return;
        }

        if (_placingText)
        {
            _placingText = false;

            var (endX, endY) = _selection.ClampPoint(x, y);
            var box = CaptureRect.FromPoints(_textAnchorX, _textAnchorY, endX, endY);

            // A drag too small to have been meant as a box is a click. Without
            // the slack every click becomes a two-pixel-wide label.
            if (box.Width < TextBoxReach || box.Height < TextBoxReach)
            {
                TextPlaced?.Invoke(this, (_textAnchorX, _textAnchorY, null));
                return;
            }

            TextPlaced?.Invoke(this, (box.X, box.Y, box.Width));
            return;
        }

        if (_gesture.Release(e.GetPosition(this)))
        {
            return;
        }

        if (_mode == OverlayMode.Drawing)
        {
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

    /// <summary>
    /// Whether these modifiers mean "the object under the pointer", for the
    /// wheel and for the cursor that hints at it.
    /// </summary>
    private static bool Grabbing(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Alt);

    /// <summary>The cursor says what the next press will do.</summary>
    private void UpdateCursor(int x, int y, KeyModifiers modifiers)
    {
        // Ctrl over a stamped glyph picks it up, whatever tool is armed. The
        // cursor is the only place that says so.
        if (Grabbing(modifiers) && _gesture.Over(x, y))
        {
            Cursor = Move;
            return;
        }

        if (_mode == OverlayMode.Typing)
        {
            Cursor = Caret;
            return;
        }

        if (_mode == OverlayMode.Drawing)
        {
            Cursor = Aim(x, y);
            return;
        }

        Cursor = SelectionGrips.HitTest(_selection, x, y, GripReach) switch
        {
            SelectionGrip.TopLeft or SelectionGrip.BottomRight => Corner,
            SelectionGrip.TopRight or SelectionGrip.BottomLeft => AntiCorner,
            SelectionGrip.Left or SelectionGrip.Right => WestEast,
            SelectionGrip.Top or SelectionGrip.Bottom => NorthSouth,
            SelectionGrip.Inside => Move,
            _ => Aim(x, y),
        };
    }

    /// <summary>
    /// The crosshair that reads against what is under it: light on a dark
    /// screenshot, dark on a light one. The pixel is already being sampled for
    /// the magnifier, so this costs a comparison.
    /// </summary>
    private Cursor Aim(int x, int y)
    {
        _darkInk = Crosshair.PrefersDark(Surface.LuminanceAround(x, y, CursorReach), _darkInk);

        return Crosshair.For(_cursorStyle, _darkInk);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Before base, and before everything else. Enter has to mean a new line
        // rather than a copy while a caret is up, and base.OnKeyDown gets the
        // built-in handling in first if it is called first.
        if (TypingActive?.Invoke() == true)
        {
            TextKeyPressed?.Invoke(this, e);

            // Handled means the session ate the key. Anything else has to reach
            // base: the Win32 backend only calls TranslateMessage for keys that
            // came back unhandled, and without that call there is no WM_CHAR
            // and no typing at all. Found the hard way - the caret was there
            // and every letter went nowhere.
            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }

            // Either way the overlay's own shortcuts stay out of it: bare C is
            // a letter while a caret is up, not a colour pick.
            return;
        }

        base.OnKeyDown(e);

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            // One step of undo per press: the open card first, then the tool,
            // and only then the overlay itself.
            // Before the rest of the ladder: a glyph in mid-air is the most
            // recent thing started, so it is the first thing Esc takes back.
            case Key.Escape when _gesture.CancelCarry():
                break;

            case Key.Escape when StyleCard.IsVisible || EmojiCard.IsVisible:
                StyleCard.Close();
                EmojiCard.Close();
                break;

            case Key.Escape when _mode == OverlayMode.Drawing:
                ToolCleared?.Invoke(this, EventArgs.Empty);
                break;

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

            // Bare C, so it cannot be reached while Ctrl is held for a copy.
            case Key.C:
                ColourRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Left or Key.Right or Key.Up or Key.Down:
                Nudge(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                break;
        }
    }

    /// <summary>
    /// Characters, as the input method finally decided them. The first handler
    /// of its kind in this project: nothing here ever wanted typing before.
    /// </summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (TypingActive?.Invoke() == true && !string.IsNullOrEmpty(e.Text))
        {
            TextEntered?.Invoke(this, e.Text);
            e.Handled = true;

            return;
        }

        base.OnTextInput(e);
    }

    /// <summary>
    /// Moves the whole selection by a pixel. There is no notion of an active
    /// edge in the model, and inventing one for the arrow keys would be a
    /// bigger change than the keys are worth.
    /// </summary>
    private void Nudge(Key key, bool fast)
    {
        if (_selection.IsEmpty || _mode == OverlayMode.Drawing)
        {
            return;
        }

        var step = fast ? NudgeStepFast : NudgeStep;

        var (dx, dy) = key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            _ => (0, step),
        };

        var moved = SelectionGrips.Apply(_selection, SelectionGrip.Inside, dx, dy, _frameBounds);
        SelectionChanged?.Invoke(this, moved);
        SelectionSettled?.Invoke(this, moved);
    }

    /// <summary>
    /// Somebody pressed C. Which window answers is not this window's business:
    /// clicking the toolbar moves the keyboard to the window the toolbar is in,
    /// and on two monitors that is not the window holding the magnifier.
    /// </summary>
    public event EventHandler? ColourRequested;

    /// <summary>
    /// The colour under this window's crosshair, as text, in the clipboard.
    /// False when this window has no magnifier up and therefore no colour.
    /// </summary>
    public bool TryCopyColour()
    {
        if (Surface.MagnifierAt is not { } at || Surface.ColorAt(at.X, at.Y) is not { } colour)
        {
            return false;
        }

        ColourCopyRequested?.Invoke(this, $"#{colour.Red:X2}{colour.Green:X2}{colour.Blue:X2}");
        Loupe.ShowCopied(colour);

        return true;
    }
}
