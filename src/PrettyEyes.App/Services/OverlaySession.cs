using PrettyEyes.App.Views;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Platform;
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
    private bool _closed;

    public OverlaySession(AppServices services) => _services = services;

    public event EventHandler? Finished;

    public Document? Document { get; private set; }

    public void Start(CaptureResult capture)
    {
        _closed = false;
        Document = new Document(capture.Image, capture.Bounds);

        foreach (var monitor in capture.Layout.Monitors)
        {
            var window = new OverlayWindow();
            window.SelectionChanged += OnSelectionChanged;
            window.Cancelled += (_, _) => Close();
            window.UndoRequested += OnUndoRequested;

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

    public void Redraw()
    {
        if (Document is null)
        {
            return;
        }

        foreach (var window in _windows)
        {
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

        // The frame is roughly 29 MB on two 2K monitors - one leak per hotkey
        // press adds up fast.
        Document?.Dispose();
        Document = null;

        Finished?.Invoke(this, EventArgs.Empty);
    }
}
