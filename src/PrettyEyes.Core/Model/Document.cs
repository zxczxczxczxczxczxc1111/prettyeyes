using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Model;

/// <summary>
/// A capture being edited: the frozen screen, the chosen region and the
/// annotations drawn on top. Owns the source image.
/// </summary>
public sealed class Document : IDisposable
{
    private readonly List<IAnnotation> _annotations = [];

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

    public void Add(IAnnotation annotation)
    {
        _annotations.Add(annotation);
        _snapshot = null;
    }

    public bool Undo()
    {
        if (_annotations.Count == 0)
        {
            return false;
        }

        _annotations.RemoveAt(_annotations.Count - 1);
        _snapshot = null;

        return true;
    }

    /// <summary>
    /// Drops every annotation. Starting the selection over means starting over:
    /// shapes drawn for the previous region have no meaning in the new one.
    /// </summary>
    public void Clear()
    {
        _annotations.Clear();
        _snapshot = null;
    }

    /// <summary>
    /// A frozen copy for the render thread. Avalonia runs ICustomDrawOperation
    /// off the UI thread, so iterating the live list races with editing.
    ///
    /// Cached until the list changes: this is asked for on every rendered
    /// frame, and dragging a frame renders as fast as the mouse reports.
    /// </summary>
    public IReadOnlyList<IAnnotation> SnapshotAnnotations() => _snapshot ??= _annotations.ToArray();

    public void Dispose() => Source.Dispose();
}
