using PrettyEyes.App.Views;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
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
    private DesktopLayout? _layout;
    private ToolKind? _activeTool;
    private bool _toolbarShown;
    private bool _closed;

    public OverlaySession(AppServices services) => _services = services;

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
            window.CopyRequested += OnCopyClicked;
            window.SaveRequested += OnSaveClicked;
            window.ToolFactory = () => _activeTool is null ? null : CreateTool(_activeTool.Value);
            window.ToolbarControl.ToolPicked += OnToolPicked;
            window.ToolbarControl.UndoClicked += OnUndoRequested;
            window.ToolbarControl.CopyClicked += OnCopyClicked;
            window.ToolbarControl.SaveClicked += OnSaveClicked;

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
        window.CopyRequested -= OnCopyClicked;
        window.SaveRequested -= OnSaveClicked;
        window.ToolFactory = null;
        window.ToolbarControl.ToolPicked -= OnToolPicked;
        window.ToolbarControl.UndoClicked -= OnUndoRequested;
        window.ToolbarControl.CopyClicked -= OnCopyClicked;
        window.ToolbarControl.SaveClicked -= OnSaveClicked;
    }

    private void OnCancelled(object? sender, EventArgs e) => Close();

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
        _toolbarShown = true;
        PlaceToolbar(selection);
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

    private void OnToolPicked(object? sender, ToolKind? kind)
    {
        _activeTool = kind;

        // Only the visible toolbar raised this, but the others have to agree:
        // the selection can move to another monitor mid-session.
        foreach (var window in _windows)
        {
            window.ToolbarControl.SetActive(kind);
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

        _activeTool = null;
        _toolbarShown = false;
        Document.Selection = CaptureRect.Empty;
        Document.Clear();

        foreach (var window in _windows)
        {
            window.ToolbarControl.SetActive(null);
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

    private async void OnCopyClicked(object? sender, EventArgs e) => await SendSafelyAsync(_services.Clipboard);

    private async void OnSaveClicked(object? sender, EventArgs e) => await SendSafelyAsync(_services.File);

    /// <summary>
    /// An async event handler is the one place where an exception has nowhere
    /// to go, so it is caught here and shown instead of killing the process.
    /// </summary>
    private async Task SendSafelyAsync(IImageSink sink)
    {
        try
        {
            await SendAsync(sink);
        }
        catch (Exception ex)
        {
            // An async void handler is the one place where an exception has
            // nowhere to go: it would take the whole process with it. Even a
            // COMException from the clipboard has to end up on screen instead.
            Log.Default.Error("вывод снимка не удался", ex);
            SetTopmost(true);
            ShowError(ex is IOException or UnauthorizedAccessException
                ? ex.Message
                : "Не удалось отдать снимок. Подробности в журнале.");
        }
    }

    private async Task SendAsync(IImageSink sink)
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
            using var image = DocumentRenderer.Render(Document);
            var result = await sink.SendAsync(image, CancellationToken.None);

            switch (result)
            {
                case SinkResult.Sent:
                    Close();
                    break;
                case SinkResult.Cancelled:
                    SetTopmost(true);
                    break;
                case SinkResult.Failed:
                    SetTopmost(true);

                    // Work in progress must survive a failed save.
                    ShowError("Не удалось сохранить снимок. Попробуй ещё раз.");
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

    private static ITool CreateTool(ToolKind kind) => kind switch
    {
        ToolKind.Blur => new BlurTool(),
        ToolKind.Arrow => new ArrowTool(),
        ToolKind.Line => new LineTool(),
        ToolKind.Rectangle => new RectangleTool(),
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

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        foreach (var window in _windows)
        {
            Unsubscribe(window);
        }

        _services.OverlayWindows.Release();
        _windows = [];
        _layout = null;
        _toolbarShown = false;

        // The frame is roughly 29 MB on two 2K monitors - one leak per hotkey
        // press adds up fast.
        Document?.Dispose();
        Document = null;

        Finished?.Invoke(this, EventArgs.Empty);
    }
}
