using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Model;

/// <summary>
/// A capture being edited: the frozen screen, the chosen region and the
/// annotations drawn on top. Owns the source image.
/// </summary>
public sealed class Document : IDisposable
{
    /// <summary>
    /// How many steps back it is possible to go. A screenshot is edited for
    /// seconds and the entries are a handful of small objects each, so the cap
    /// is here to bound a runaway rather than to save anything worth saving.
    /// </summary>
    private const int HistoryDepth = 100;

    private readonly List<IAnnotation> _annotations = [];

    /// <summary>
    /// The whole list as it was before each change. Storing states rather than
    /// operations means a move undoes exactly like a draw, and neither has to
    /// know how to reverse itself.
    /// </summary>
    private readonly List<IAnnotation[]> _history = [];

    private IAnnotation? _detached;

    /// <summary>
    /// Which annotation the current run of resize steps belongs to, by index,
    /// or -1 between runs.
    /// </summary>
    private int _resizing = -1;

    private IReadOnlyList<IAnnotation>? _snapshot;

    public Document(SKImage source, CaptureRect sourceBounds)
    {
        Source = source;
        SourceBounds = sourceBounds;
    }

    public SKImage Source { get; }

    /// <summary>
    /// Where the captured frame sits in virtual-desktop coordinates.
    /// </summary>
    public CaptureRect SourceBounds { get; }

    public CaptureRect Selection { get; set; }

    public IReadOnlyList<IAnnotation> Annotations => _annotations;

    /// <summary>
    /// Lifted off the picture for the moment, because it is being dragged.
    /// The overlay draws it under the pointer instead; leaving it in the list
    /// would show the same glyph twice for the length of the drag.
    /// </summary>
    public IAnnotation? Detached
    {
        get => _detached;
        set
        {
            _detached = value;
            _snapshot = null;
        }
    }

    public void Add(IAnnotation annotation)
    {
        Remember();
        _annotations.Add(annotation);
        _snapshot = null;
    }

    /// <summary>
    /// Puts an annotation somewhere else, keeping its place in the stack: a
    /// glyph that jumps in front of the arrow it was behind has moved in a way
    /// nobody asked for. Does nothing if it is not in the list.
    /// </summary>
    public bool Move(IMovable annotation, int dx, int dy)
    {
        var index = _annotations.IndexOf(annotation);

        if (index < 0 || (dx == 0 && dy == 0))
        {
            return false;
        }

        Remember();
        _annotations[index] = annotation.MovedBy(dx, dy);
        _snapshot = null;

        return true;
    }

    /// <summary>
    /// Grows or shrinks one annotation in place. A run of steps on the same one
    /// counts as a single change, so a wheel spun ten notches is one undo and
    /// not ten.
    /// </summary>
    public bool Resize(IMovable annotation, int steps)
    {
        var index = _annotations.IndexOf(annotation);

        if (index < 0 || annotation.ResizedBy(steps) is not { } resized)
        {
            return false;
        }

        if (_resizing != index)
        {
            Remember();
            _resizing = index;
        }

        _annotations[index] = resized;
        _snapshot = null;

        return true;
    }

    /// <summary>
    /// The topmost thing under the point that can be dragged, or null. Topmost
    /// because that is the one the eye picks when two overlap.
    /// </summary>
    public IMovable? MovableAt(int x, int y)
    {
        for (var i = _annotations.Count - 1; i >= 0; i--)
        {
            if (_annotations[i] is IMovable movable && movable.Bounds.Contains(x, y))
            {
                return movable;
            }
        }

        return null;
    }

    public bool Undo()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        _annotations.Clear();
        _annotations.AddRange(_history[^1]);
        _history.RemoveAt(_history.Count - 1);
        _resizing = -1;
        _snapshot = null;

        return true;
    }

    private void Remember()
    {
        // Any other change ends the run: the next wheel step after drawing
        // something else is a change of its own.
        _resizing = -1;

        _history.Add([.. _annotations]);

        if (_history.Count > HistoryDepth)
        {
            _history.RemoveAt(0);
        }
    }

    /// <summary>
    /// Drops every annotation. Starting the selection over means starting over:
    /// shapes drawn for the previous region have no meaning in the new one.
    /// </summary>
    public void Clear()
    {
        _annotations.Clear();
        _history.Clear();
        _detached = null;
        _resizing = -1;
        _snapshot = null;
    }

    /// <summary>
    /// A frozen copy for the render thread. Avalonia runs ICustomDrawOperation
    /// off the UI thread, so iterating the live list races with editing.
    ///
    /// Cached until the list changes: this is asked for on every rendered
    /// frame, and dragging a frame renders as fast as the mouse reports.
    /// </summary>
    public IReadOnlyList<IAnnotation> SnapshotAnnotations() =>
        _snapshot ??= _detached is null
            ? [.. _annotations]
            : [.. _annotations.Where(annotation => !ReferenceEquals(annotation, _detached))];

    public void Dispose() => Source.Dispose();
}
