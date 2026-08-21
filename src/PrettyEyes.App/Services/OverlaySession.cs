using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Text;
using PrettyEyes.Core.Tools;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Services;

/// <summary>
/// One capture from hotkey to close: opens an overlay window per monitor and
/// keeps a single shared selection across all of them.
/// </summary>
public sealed class OverlaySession
{
    private readonly AppServices _services;
    private IReadOnlyList<OverlayWindow> _windows = [];

    /// <summary>The window the pointer was last seen over, magnifier and all.</summary>
    private OverlayWindow? _pointerWindow;
    private DesktopLayout? _layout;
    private ToolKind? _activeTool;
    private readonly ToolStyles _styles;
    private string? _emoji;
    private bool _toolbarShown;
    private bool _closed;

    public OverlaySession(AppServices services)
    {
        _services = services;
        _styles = new ToolStyles(services.Settings.ToolStyles ?? []);
        _emoji = services.Settings.Emoji;
    }

    public event EventHandler? Finished;

    public Document? Document { get; private set; }

    public void Start(CaptureResult capture)
    {
        using var scope = Log.Default.Scope("overlay");

        _closed = false;
        _layout = capture.Layout;
        Document = new Document(capture.Image, capture.Bounds);

        // Windows come from the pool: building them here cost 105 ms on the
        // first capture, which the user spends staring at an unfrozen screen.
        _windows = _services.OverlayWindows.Take(capture.Layout);

        for (var i = 0; i < _windows.Count; i++)
        {
            var window = _windows[i];

            window.SelectionChanged += OnSelectionChanged;
            window.SelectionSettled += OnSelectionSettled;
            window.Cancelled += OnCancelled;
            window.UndoRequested += OnUndoRequested;
            window.AnnotationDrawn += OnAnnotationDrawn;
            window.AnnotationMoved += OnAnnotationMoved;
            window.AnnotationResized += OnAnnotationResized;
            window.CopyRequested += OnCopyClicked;
            window.SaveRequested += OnSaveRequested;
            window.ColourCopyRequested += OnColourCopyRequested;
            window.ToolCleared += OnToolCleared;
            window.ToolbarControl.StyleRequested += OnStyleRequested;
            window.StyleCardControl.StyleChanged += OnStyleChanged;
            window.EmojiCardControl.Picked += OnEmojiPicked;
            window.EmojiCardControl.Restore(_services.Settings.RecentEmoji ?? []);

            if (_emoji is not null)
            {
                window.ToolbarControl.ShowGlyph(_emoji);
            }
            // The window comes out of a pool and remembers the highlight from
            // the last capture. The session is new and remembers nothing. Say
            // it out loud.
            window.ToolbarControl.SetActive(_activeTool);
            window.ToolbarControl.ShowStyles(_styles);
            window.ToolbarControl.ShowTools(new ToolVisibility(_services.Settings.Tools));
            // Text is armed like any other tool but builds nothing: its whole
            // gesture is deciding where the caret goes.
            window.ToolFactory = () =>
                _activeTool is null or ToolKind.Text ? null : CreateTool(_activeTool.Value);
            window.ToolbarControl.ToolPicked += OnToolPicked;
            window.ToolbarControl.UndoClicked += OnUndoRequested;
            window.ToolbarControl.CopyClicked += OnCopyClicked;
            window.ToolbarControl.SaveClicked += OnSaveRequested;

            window.TypingActive = () => _typing is not null;
            window.TextPointerPressed = OnTextPointerPressed;
            window.TextKeyPressed += OnTextKeyPressed;
            window.TextEntered += OnTextEntered;
            window.TextPlaced += OnTextPlaced;
            window.TextSelectionDragged += OnTextSelectionDragged;
            window.TextBoxDragged += OnTextBoxDragged;
            window.TextEditRequested += OnTextEditRequested;
            window.Deactivated += OnWindowDeactivated;

            window.PointerSeen += OnPointerSeen;
            window.ColourRequested += OnColourRequested;
            window.SetCursorStyle(_services.Settings.Cursor);
            window.SetMagnifierEnabled(_services.Settings.ShowMagnifier);
            window.SetMagnifierGrid(_services.Settings.MagnifierGrid);
            window.SetExportStyle(_services.Settings.Export ?? ExportStyle.None);
            window.PlaceOn(capture.Layout.Monitors[i], Document);
        }

        _windows.FirstOrDefault()?.Activate();
    }

    /// <summary>
    /// Undone on close: the windows outlive the session, and a handler left
    /// behind would answer for a document that is already disposed.
    /// </summary>
    private void Unsubscribe(OverlayWindow window)
    {
        window.SelectionChanged -= OnSelectionChanged;
        window.SelectionSettled -= OnSelectionSettled;
        window.Cancelled -= OnCancelled;
        window.UndoRequested -= OnUndoRequested;
        window.AnnotationDrawn -= OnAnnotationDrawn;
        window.AnnotationMoved -= OnAnnotationMoved;
        window.AnnotationResized -= OnAnnotationResized;
        window.CopyRequested -= OnCopyClicked;
        window.SaveRequested -= OnSaveRequested;
        window.ColourCopyRequested -= OnColourCopyRequested;
        window.ToolCleared -= OnToolCleared;
        window.ToolbarControl.StyleRequested -= OnStyleRequested;
        window.StyleCardControl.StyleChanged -= OnStyleChanged;
        window.EmojiCardControl.Picked -= OnEmojiPicked;
        window.ToolFactory = null;
        window.ToolbarControl.ToolPicked -= OnToolPicked;
        window.ToolbarControl.UndoClicked -= OnUndoRequested;
        window.ToolbarControl.CopyClicked -= OnCopyClicked;
        window.ToolbarControl.SaveClicked -= OnSaveRequested;
        window.TypingActive = null;
        window.TextPointerPressed = null;
        window.TextKeyPressed -= OnTextKeyPressed;
        window.TextEntered -= OnTextEntered;
        window.TextPlaced -= OnTextPlaced;
        window.TextSelectionDragged -= OnTextSelectionDragged;
        window.TextBoxDragged -= OnTextBoxDragged;
        window.TextEditRequested -= OnTextEditRequested;
        window.Deactivated -= OnWindowDeactivated;
        window.PointerSeen -= OnPointerSeen;
        window.ColourRequested -= OnColourRequested;
    }

    /// <summary>
    /// One magnifier at a time, on the monitor the pointer is actually on.
    /// Setting a property to the value it already holds costs nothing, so this
    /// runs on every pointer move without a repaint to show for it.
    /// </summary>
    private void OnPointerSeen(object? sender, EventArgs e)
    {
        _pointerWindow = sender as OverlayWindow;

        foreach (var window in _windows)
        {
            if (!ReferenceEquals(window, sender))
            {
                window.DropMagnifier();
            }
        }
    }

    /// <summary>
    /// C reads the colour under the magnifier, and the magnifier belongs to the
    /// window the pointer is over. That is not the window with the keyboard as
    /// soon as the toolbar has been clicked once, so the key is answered here
    /// rather than where it was pressed.
    /// </summary>
    private void OnColourRequested(object? sender, EventArgs e)
    {
        if (_pointerWindow?.TryCopyColour() == true)
        {
            return;
        }

        (sender as OverlayWindow)?.TryCopyColour();
    }

    private void OnCancelled(object? sender, EventArgs e) => Close();

    /// <summary>
    /// The colour goes in as text and the overlay stays open: reading a colour
    /// off the screen is something people do several times in a row.
    /// </summary>
    private async void OnColourCopyRequested(object? sender, string hex)
    {
        try
        {
            await _services.Host.Clipboard!.SetTextAsync(hex);
        }
        catch (Exception error)
        {
            Log.Default.Error("не удалось скопировать цвет", error);
        }
    }

    /// <summary>
    /// Every monitor window feeds the same selection, so dragging across a
    /// monitor edge keeps working.
    /// </summary>
    private void OnSelectionChanged(object? sender, CaptureRect selection)
    {
        if (Document is null)
        {
            return;
        }

        var previous = Document.Selection;
        Document.Selection = selection;

        for (var i = 0; i < _windows.Count; i++)
        {
            // A monitor neither selection touches keeps the veil it already
            // has; repainting all of them triples the work on three screens.
            if (_layout is not null && !Touches(_layout.Monitors[i].Bounds, previous, selection))
            {
                continue;
            }

            _windows[i].ShowSelection(selection);
        }

        // Once the toolbar is up it follows the frame; leaving it behind and
        // teleporting it at the end of the gesture reads as a glitch.
        if (_toolbarShown)
        {
            PlaceToolbar(selection);
        }
    }

    /// <summary>
    /// Whether a monitor sees any part of the change. Empty selections count as
    /// touching everything: that is the frame appearing or being dropped.
    /// </summary>
    private static bool Touches(CaptureRect monitor, CaptureRect before, CaptureRect after) =>
        before.IsEmpty || after.IsEmpty
        || !monitor.Intersect(before).IsEmpty
        || !monitor.Intersect(after).IsEmpty;

    /// <summary>
    /// Panel and chip belong to the finished gesture: while the pointer is down
    /// they would just trail after the cursor.
    /// </summary>
    private void OnSelectionSettled(object? sender, CaptureRect selection)
    {
        // Read before the assignment: this fires on every finished drag, and
        // only the first one is the toolbar appearing.
        var chosen = DefaultTool.Apply(_services.Settings.DefaultTool, _toolbarShown);

        _toolbarShown = true;
        PlaceToolbar(selection);

        if (chosen is not null)
        {
            // Through the session, like any other pick: the toolbar is a
            // display and stopped deciding anything in task 4.
            OnToolPicked(this, chosen);
        }
    }

    /// <summary>
    /// The toolbar belongs to the monitor holding the selection's bottom-right
    /// corner - otherwise every window would draw its own copy.
    /// </summary>
    private void PlaceToolbar(CaptureRect selection)
    {
        if (_layout is null)
        {
            return;
        }

        var owner = selection.IsEmpty
            ? null
            : _layout.MonitorAt(selection.Right - 1, selection.Bottom - 1)
              ?? _layout.MonitorAt(selection.X, selection.Y);

        for (var i = 0; i < _windows.Count; i++)
        {
            if (owner is not null && _layout.Monitors[i].DeviceId == owner.DeviceId)
            {
                _windows[i].PlaceToolbar(selection);
            }
            else
            {
                _windows[i].HideToolbar();
            }
        }
    }

    /// <summary>
    /// Keys arrive at whichever overlay currently holds focus, so undo is
    /// handled at session level rather than per window.
    /// </summary>
    private void OnUndoRequested(object? sender, EventArgs e)
    {
        if (Document?.Undo() == true)
        {
            Redraw();
        }
    }

    private void OnToolCleared(object? sender, EventArgs e) => OnToolPicked(sender, null);

    /// <summary>
    /// Right click on a tool. The card belongs to the window whose toolbar is
    /// showing, which is the one that raised this.
    /// </summary>
    private void OnStyleRequested(object? sender, ToolKind kind)
    {
        foreach (var window in _windows)
        {
            var owner = ReferenceEquals(window.ToolbarControl, sender);

            window.HideStyleCard();
            window.HideEmojiCard();

            if (!owner)
            {
                continue;
            }

            // Emoji has a grid of glyphs where the others have colours.
            if (kind == ToolKind.Emoji)
            {
                window.ShowEmojiCard();
            }
            else
            {
                window.ShowStyleCard(kind, _styles.For(kind));
            }
        }
    }

    /// <summary>
    /// A glyph was chosen. The grid closes: unlike colour and thickness, this
    /// is one choice and the next thing to do is stamp it.
    /// </summary>
    private void OnEmojiPicked(object? sender, string code)
    {
        _emoji = code;

        var recent = (sender as EmojiPickerView)?.Recent.ToList() ?? [];

        foreach (var window in _windows)
        {
            window.ToolbarControl.ShowGlyph(code);
            window.HideEmojiCard();
            window.ToolbarControl.SetActive(ToolKind.Emoji);
            window.SetToolActive(active: true);
        }

        _activeTool = ToolKind.Emoji;

        var settings = _services.Settings with { Emoji = code, RecentEmoji = recent };
        _services.Settings = settings;

        if (!_services.SettingsStore.Save(settings))
        {
            Log.Default.Info("не удалось сохранить выбор эмодзи");
        }
    }

    /// <summary>
    /// Applies to whatever is drawn next. Shapes already on the screenshot keep
    /// the style they were drawn with: there is no way to select one yet.
    /// </summary>
    private void OnStyleChanged(object? sender, (ToolKind Kind, ToolStyle Style) change)
    {
        _styles.Set(change.Kind, change.Style);

        foreach (var window in _windows)
        {
            window.ToolbarControl.ShowStyles(_styles);
        }

        var settings = _services.Settings with
        {
            ToolStyles = _styles.Stored.ToDictionary(pair => pair.Key, pair => pair.Value),
        };

        _services.Settings = settings;

        if (!_services.SettingsStore.Save(settings))
        {
            Log.Default.Info("не удалось сохранить стиль инструмента");
        }
    }

    private void OnToolPicked(object? sender, ToolKind? kind)
    {
        // Emoji without a glyph has nothing to stamp: the grid opens instead of
        // the tool arming itself with nothing.
        if (kind == ToolKind.Emoji && _emoji is null)
        {
            OnStyleRequested(sender, ToolKind.Emoji);
            return;
        }

        // Picking another tool finishes whatever is being typed. Leaving the
        // caret up while the arrow is armed means the next keystroke goes
        // somewhere the user is no longer looking.
        CommitText();

        _activeTool = kind;

        // Only the visible toolbar raised this, but the others have to agree:
        // the selection can move to another monitor mid-session.
        foreach (var window in _windows)
        {
            window.ToolbarControl.SetActive(kind);
            window.TextToolArmed = kind == ToolKind.Text;
            window.SetToolActive(kind is not null);
        }
    }

    /// <summary>
    /// Pressing the hotkey again drops the tool and the current selection
    /// without taking a new capture: the screen is already frozen.
    /// </summary>
    public void Restart()
    {
        if (Document is null)
        {
            return;
        }

        CancelText();

        _activeTool = null;
        _toolbarShown = false;
        Document.Selection = CaptureRect.Empty;
        Document.Clear();

        foreach (var window in _windows)
        {
            window.ToolbarControl.SetActive(null);
            window.TextToolArmed = false;
            window.SetToolActive(active: false);
            window.ResetSelection();
            window.HideToolbar();
        }
    }

    private void OnAnnotationDrawn(object? sender, IAnnotation annotation)
    {
        Document?.Add(annotation);
        Redraw();
    }

    /// <summary>
    /// A carried glyph was put down. A drop that went nowhere still redraws:
    /// that is how a cancelled carry gets the glyph back on screen.
    /// </summary>
    private void OnAnnotationMoved(object? sender, (IMovable Annotation, int Dx, int Dy) move)
    {
        Document?.Move(move.Annotation, move.Dx, move.Dy);
        Redraw();
    }

    private void OnAnnotationResized(object? sender, (IMovable Annotation, int Steps) change)
    {
        if (Document?.Resize(change.Annotation, change.Steps) == true)
        {
            Redraw();
        }
    }

    /// <summary>
    /// Copy always fills the clipboard, and with autosave on it also drops a
    /// file in the folder. The file is a bonus, so the clipboard goes first:
    /// a disk that has gone away must not cost the user their screenshot.
    /// </summary>
    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        var autosave = _services.Settings.Save?.Ready == true;

        await SendSafelyAsync(_services.Clipboard, closeOnSuccess: !autosave);

        if (autosave)
        {
            await SendSafelyAsync(
                _services.Folder,
                failure: "Скриншот в буфере, но в папку не сохранился. Проверь папку в настройках.");
        }
    }

    /// <summary>
    /// With autosave on this writes silently; holding Shift asks for a place
    /// anyway, because "somewhere else, just this once" has to stay possible.
    /// </summary>
    /// <summary>
    /// Saving on purpose always asks where. Quick save is a different feature
    /// with its own switch, and it has already put a copy in the folder by the
    /// time this button is pressed; a save button that silently drops the file
    /// somewhere leaves no way to put one screenshot anywhere else.
    /// </summary>
    private async void OnSaveRequested(object? sender, EventArgs e) =>
        await SendSafelyAsync(_services.File);

    /// <summary>
    /// An async event handler is the one place where an exception has nowhere
    /// to go, so it is caught here and shown instead of killing the process.
    /// </summary>
    private async Task SendSafelyAsync(IImageSink sink, bool closeOnSuccess = true, string? failure = null)
    {
        try
        {
            await SendAsync(sink, closeOnSuccess, failure);
        }
        catch (Exception ex)
        {
            // An async void handler is the one place where an exception has
            // nowhere to go: it would take the whole process with it. Even a
            // COMException from the clipboard has to end up on screen instead.
            Log.Default.Error("вывод скриншота не удался", ex);
            SetTopmost(true);
            ShowError(ex is IOException or UnauthorizedAccessException
                ? ex.Message
                : "Не удалось отдать скриншот. Подробности в журнале.");
        }
    }

    private async Task SendAsync(IImageSink sink, bool closeOnSuccess = true, string? failure = null)
    {
        if (Document is null)
        {
            return;
        }

        // The overlays are Topmost, and a system dialog would open underneath
        // them. Drop it for the duration and put it back if the user cancels.
        SetTopmost(false);

        try
        {
            // Transparency reaches the sink as it was drawn. The clipboard
            // writes PNG next to the DIB and decides for itself what to do with
            // an alpha channel the DIB cannot carry.
            var style = _services.Settings.Export ?? ExportStyle.None;

            using var image = DocumentRenderer.Render(Document, style);

            var result = await sink.SendAsync(image, CancellationToken.None);

            switch (result)
            {
                case SinkResult.Sent:
                    if (closeOnSuccess)
                    {
                        Close();
                    }

                    break;
                case SinkResult.Cancelled:
                    SetTopmost(true);
                    break;
                case SinkResult.Failed:
                    SetTopmost(true);

                    // Work in progress must survive a failed save.
                    ShowError(failure ?? "Не удалось сохранить скриншот. Попробуй ещё раз.");
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            SetTopmost(true);
            ShowError(ex.Message);
        }
    }

    private void SetTopmost(bool value)
    {
        foreach (var window in _windows)
        {
            window.Topmost = value;
        }
    }

    private void ShowError(string message)
    {
        foreach (var window in _windows)
        {
            window.ShowError(message);
        }
    }

    /// <summary>
    /// Half of the caret blink. The number Windows itself uses, so a caret in
    /// the overlay does not beat against every other caret on the screen.
    /// </summary>
    private static readonly TimeSpan Blink = TimeSpan.FromMilliseconds(530);

    /// <summary>The label being typed right now, or null. At most one.</summary>
    private TextEditing? _typing;

    private DispatcherTimer? _blink;

    /// <summary>
    /// A place was picked for a new label: a click, or a box dragged out. The
    /// click case still wraps, at the right edge of the region - a line running
    /// off the monitor is not a label.
    /// </summary>
    private void OnTextPlaced(object? sender, (int X, int Y, int? MaxWidth) placed)
    {
        if (sender is not OverlayWindow window || Document is null)
        {
            return;
        }

        var style = _styles.For(ToolKind.Text);
        var padding = style.TextPadding;

        if (placed.MaxWidth is { } width)
        {
            BeginText(window, placed.X, placed.Y, Math.Max(1, width - (padding * 2)), original: null);
            return;
        }

        // Placed by the glyphs rather than by the box: the text starts where
        // the pointer was, and the plate grows around it.
        BeginText(
            window,
            placed.X - padding,
            placed.Y - padding,
            Math.Max(1, Document.Selection.Right - placed.X - padding),
            original: null);
    }

    /// <summary>Double click on a finished label puts the caret back into it.</summary>
    private void OnTextEditRequested(object? sender, TextAnnotation label)
    {
        if (sender is OverlayWindow window)
        {
            BeginText(window, label.Bounds.X, label.Bounds.Y, label.MaxWidth, label);
        }
    }

    private void BeginText(OverlayWindow window, int x, int y, int? maxWidth, TextAnnotation? original)
    {
        // One caret at a time. Starting a second label finishes the first, the
        // same as clicking anywhere else would.
        CommitText();

        _typing = new TextEditing
        {
            Window = window,
            Editor = new TextEditor(original?.Text ?? string.Empty),
            X = x,
            Y = y,
            MaxWidth = maxWidth,
            Style = original?.Style ?? _styles.For(ToolKind.Text),
            Original = original,
        };

        if (original is not null && Document is not null)
        {
            // Lifted off the picture while it is edited, exactly like a glyph
            // being dragged: otherwise the old text shows through the new one.
            Document.Detached = original;
        }

        foreach (var window_ in _windows)
        {
            window_.SetTyping(ReferenceEquals(window_, window));
        }

        _blink ??= new DispatcherTimer { Interval = Blink };
        _blink.Tick -= OnBlink;
        _blink.Tick += OnBlink;

        RestartBlink();
        Redraw();
    }

    private void OnBlink(object? sender, EventArgs e)
    {
        if (_typing is not { } typing)
        {
            return;
        }

        typing.CaretOn = !typing.CaretOn;
        RefreshText();
    }

    /// <summary>
    /// Solid again and counting from zero. A caret that keeps blinking through
    /// a burst of typing hides itself exactly when it is being watched.
    /// </summary>
    private void RestartBlink()
    {
        if (_typing is not { } typing || _blink is null)
        {
            return;
        }

        typing.CaretOn = true;
        _blink.Stop();
        _blink.Start();
        RefreshText();
    }

    /// <summary>
    /// Draws the label as it stands, on the window holding the caret and on no
    /// other. The neighbours get null, or a label typed on one monitor would be
    /// painted at the same coordinates on the next one.
    /// </summary>
    private void RefreshText()
    {
        if (_typing is not { } typing)
        {
            foreach (var window in _windows)
            {
                window.ShowTextPreview(null);
            }

            return;
        }

        var preview = new TextPreview(typing.Label, typing.Editor, typing.CaretOn);

        foreach (var window in _windows)
        {
            window.ShowTextPreview(ReferenceEquals(window, typing.Window) ? preview : null);
        }
    }

    /// <summary>
    /// The box for a new label while it is still being dragged out. Drawn in
    /// the colour the text will be, so the two read as the same thing.
    /// </summary>
    private void OnTextBoxDragged(object? sender, CaptureRect box)
    {
        if (sender is not OverlayWindow window)
        {
            return;
        }

        window.ShowTextPreview(box.IsEmpty
            ? null
            : new RectangleAnnotation(box, _styles.For(ToolKind.Text).Color, 1f));
    }

    /// <summary>
    /// A press while a caret is up. Inside the label it moves the caret and
    /// starts picking characters; anywhere else - including another monitor -
    /// it finishes the label and does nothing more.
    /// </summary>
    private bool OnTextPointerPressed(int x, int y, int clickCount)
    {
        if (_typing is not { } typing)
        {
            return false;
        }

        var label = typing.Label;
        var box = label.Bounds.IsEmpty
            ? new TextPreview(label, typing.Editor, caretOn: false).Bounds
            : label.Bounds;

        if (!box.Contains(x, y))
        {
            CommitText();
            return false;
        }

        if (clickCount >= 2)
        {
            typing.Editor.SelectAll();
        }
        else
        {
            typing.Editor.MoveTo(IndexAt(typing, x, y), extend: false);
        }

        RestartBlink();

        return true;
    }

    private void OnTextSelectionDragged(object? sender, (int X, int Y) at)
    {
        if (_typing is not { } typing)
        {
            return;
        }

        typing.Editor.MoveTo(IndexAt(typing, at.X, at.Y), extend: true);
        RestartBlink();
    }

    private static int IndexAt(TextEditing typing, int x, int y)
    {
        var label = typing.Label;

        using var font = TextLayout.FontFor(typing.Style);

        return TextLayout.IndexAt(
            label.Segments,
            x - typing.X,
            y - typing.Y,
            font,
            typing.Style.TextPadding);
    }

    /// <summary>
    /// Every key while a caret is up, including the ones this window would
    /// otherwise treat as shortcuts. Enter is a new line and not a copy, C is a
    /// letter and not a colour pick, and the only way to be sure of that is to
    /// swallow the lot.
    /// </summary>
    private void OnTextKeyPressed(object? sender, KeyEventArgs e)
    {
        if (_typing is not { } typing)
        {
            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var editor = typing.Editor;
        var label = typing.Label;

        using var font = TextLayout.FontFor(typing.Style);
        var padding = typing.Style.TextPadding;

        e.Handled = true;

        switch (e.Key)
        {
            case Key.Escape:
                CancelText();
                return;

            case Key.Enter or Key.Return when control:
                CommitText();
                return;

            case Key.Enter or Key.Return:
                editor.Insert("\n");
                break;

            case Key.Back:
                editor.Backspace();
                break;

            case Key.Delete:
                editor.Delete();
                break;

            case Key.Left:
                editor.MoveBy(-1, shift);
                break;

            case Key.Right:
                editor.MoveBy(1, shift);
                break;

            case Key.Up:
                editor.MoveTo(TextLayout.Above(label.Segments, editor.Caret, font, padding), shift);
                break;

            case Key.Down:
                editor.MoveTo(TextLayout.Below(label.Segments, editor.Caret, font, padding), shift);
                break;

            case Key.Home:
                editor.MoveTo(TextLayout.LineStart(label.Segments, editor.Caret), shift);
                break;

            case Key.End:
                editor.MoveTo(TextLayout.LineEnd(label.Segments, editor.Caret), shift);
                break;

            case Key.A when control:
                editor.SelectAll();
                break;

            case Key.Z when control:
                // Only this label's own typing. With nothing left to take back
                // it stops here rather than reaching into the document: undoing
                // a shape drawn before the label was started is not what the
                // key means while the caret is blinking.
                editor.Undo();
                break;

            case Key.V when control:
                PasteAsync();
                return;
        }

        RestartBlink();
    }

    private void OnTextEntered(object? sender, string text)
    {
        if (_typing is not { } typing)
        {
            return;
        }

        using var font = TextLayout.FontFor(typing.Style);

        typing.Editor.Insert(TextEditor.Sanitize(text, font));
        RestartBlink();
    }

    /// <summary>
    /// Clipboard text, cleaned the same way typed text is. Async because the
    /// clipboard is, and swallowed on failure because a clipboard that refuses
    /// to answer must not take the label with it.
    /// </summary>
    private async void PasteAsync()
    {
        try
        {
            if (await _services.Host.Clipboard!.TryGetTextAsync() is not { Length: > 0 } text)
            {
                return;
            }

            if (_typing is not { } typing)
            {
                return;
            }

            using var font = TextLayout.FontFor(typing.Style);

            typing.Editor.Insert(TextEditor.Sanitize(text, font));
            RestartBlink();
        }
        catch (Exception error)
        {
            Log.Default.Error("не удалось вставить текст из буфера", error);
        }
    }

    /// <summary>
    /// Finishes the label. An empty one is never created, and an existing one
    /// emptied out is removed: deleting the text is how a label is deleted.
    /// </summary>
    private void CommitText()
    {
        if (_typing is not { } typing)
        {
            return;
        }

        _typing = null;
        StopTyping();

        var text = typing.Editor.Text;

        if (typing.Original is { } original)
        {
            if (text.Length == 0)
            {
                Document?.Remove(original);
            }
            else if (original.Text != text)
            {
                // One change, one undo. Remove plus add would cost two presses
                // and the second would eat the label.
                Document?.Replace(original, typing.Label);
            }
            else if (Document is not null)
            {
                Document.Detached = null;
            }
        }
        else if (text.Length > 0)
        {
            Document?.Add(typing.Label);
        }

        Redraw();
    }

    /// <summary>
    /// Escape: the label goes away and an edited one goes back to what it said
    /// before, which is what it still says - the original was never touched.
    /// </summary>
    private void CancelText()
    {
        if (_typing is null)
        {
            return;
        }

        _typing = null;
        StopTyping();

        if (Document is not null)
        {
            Document.Detached = null;
        }

        Redraw();
    }

    private void StopTyping()
    {
        _blink?.Stop();

        foreach (var window in _windows)
        {
            window.ShowTextPreview(null);
            window.SetTyping(false);
            window.SetToolActive(_activeTool is not null);
        }
    }

    /// <summary>
    /// The capture lost the keyboard altogether: alt-tab, another application,
    /// anything. The label is finished rather than lost.
    ///
    /// Posted, because a click on another monitor of this same capture
    /// deactivates one overlay and activates another, and in between them
    /// nothing of ours is active. Asking a beat later is asking about a state
    /// that has settled.
    /// </summary>
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_typing is null || _closed)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_typing is null || _closed || _windows.Any(window => window.IsActive))
                {
                    return;
                }

                CommitText();
            },
            DispatcherPriority.Background);
    }

    private ITool CreateTool(ToolKind kind) => kind switch
    {
        ToolKind.Blur => new BlurTool(),
        ToolKind.Arrow => new ArrowTool(_styles.For(kind)),
        ToolKind.Line => new LineTool(_styles.For(kind)),
        ToolKind.Rectangle => new RectangleTool(_styles.For(kind)),
        ToolKind.Pencil => new StrokeTool(_styles.For(kind)),
        ToolKind.Marker => new StrokeTool(_styles.For(kind), highlighter: true),
        ToolKind.Emoji => new EmojiTool(
            _services.Emoji.Glyph(_emoji ?? string.Empty)
                ?? throw new InvalidOperationException("Эмодзи не выбран."),
            Document?.Selection ?? CaptureRect.Empty),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tool."),
    };

    public void Redraw()
    {
        if (Document is null)
        {
            return;
        }

        foreach (var window in _windows)
        {
            // ShowSelection repaints the canvas, which is what makes a new
            // annotation appear on the neighbouring monitor as well.
            window.ShowSelection(Document.Selection);
        }
    }

    /// <summary>
    /// How long the overlay takes to disappear. Long enough to be a movement
    /// rather than a cut, short enough that nobody waits for it.
    /// </summary>
    private static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(120);

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        // Before the flag: a label half typed when the overlay is dismissed is
        // a label the user meant to keep, and the screenshot is rendered from
        // the document rather than from the preview.
        CommitText();

        _closed = true;
        _blink?.Stop();

        foreach (var window in _windows)
        {
            Unsubscribe(window);
        }

        // Faded, not cut. The screenshot is already in the clipboard by now, so
        // the tenth of a second this takes is spent after the work is done and
        // before the screen comes back - which is the moment that reads as
        // abrupt when it is not there.
        foreach (var window in _windows)
        {
            window.FadeOut();
        }

        // The frame stays alive until the windows have let go of it: they are
        // still drawing it for as long as the fade lasts, and 29 MB of pixels
        // pulled out from under a render in progress is a crash, not a leak.
        var frame = Document;

        _services.OverlayWindows.Release(
            FadeOut,
            () =>
            {
                // Blurred slices belong to the capture that is going away.
                BlurCache.Shared.Clear();

                // Roughly 29 MB on two 2K monitors: one leak per hotkey press
                // adds up fast.
                frame?.Dispose();
            });

        _windows = [];
        _layout = null;
        _toolbarShown = false;
        Document = null;

        Finished?.Invoke(this, EventArgs.Empty);
    }
}
