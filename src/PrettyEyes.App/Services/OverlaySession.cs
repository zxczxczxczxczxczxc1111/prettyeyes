using PrettyEyes.App.Views;
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
    private readonly List<OverlayWindow> _windows = [];
    private DesktopLayout? _layout;
    private ToolKind? _activeTool;
    private bool _closed;

    public OverlaySession(AppServices services) => _services = services;

    public event EventHandler? Finished;

    public Document? Document { get; private set; }

    public void Start(CaptureResult capture)
    {
        _closed = false;
        _layout = capture.Layout;
        Document = new Document(capture.Image, capture.Bounds);

        foreach (var monitor in capture.Layout.Monitors)
        {
            var window = new OverlayWindow();
            window.SelectionChanged += OnSelectionChanged;
            window.SelectionSettled += OnSelectionSettled;
            window.Cancelled += (_, _) => Close();
            window.UndoRequested += OnUndoRequested;
            window.AnnotationDrawn += OnAnnotationDrawn;
            window.ToolFactory = () => _activeTool is null ? null : CreateTool(_activeTool.Value);
            window.ToolbarControl.ToolPicked += OnToolPicked;
            window.ToolbarControl.UndoClicked += OnUndoRequested;
            window.ToolbarControl.CopyClicked += async (_, _) => await SendAsync(_services.Clipboard);
            window.ToolbarControl.SaveClicked += async (_, _) => await SendAsync(_services.File);

            _windows.Add(window);
            window.PlaceOn(monitor, Document);
        }

        _windows.FirstOrDefault()?.Activate();
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

        Document.Selection = selection;

        foreach (var window in _windows)
        {
            window.ShowSelection(selection);
        }
    }

    /// <summary>
    /// Panel and chip belong to the finished gesture: while the pointer is down
    /// they would just trail after the cursor.
    /// </summary>
    private void OnSelectionSettled(object? sender, CaptureRect selection) => PlaceToolbar(selection);

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

    private void OnToolPicked(object? sender, ToolKind kind)
    {
        _activeTool = kind;

        // Only the visible toolbar raised this, but the others have to agree:
        // the selection can move to another monitor mid-session.
        foreach (var window in _windows)
        {
            window.ToolbarControl.SetActive(kind);
            window.SetToolActive(active: true);
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
        Document.Selection = CaptureRect.Empty;

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
            window.Close();
        }

        _windows.Clear();
        _layout = null;

        // The frame is roughly 29 MB on two 2K monitors - one leak per hotkey
        // press adds up fast.
        Document?.Dispose();
        Document = null;

        Finished?.Invoke(this, EventArgs.Empty);
    }
}
