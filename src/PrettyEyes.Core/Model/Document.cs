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

    public void Add(IAnnotation annotation) => _annotations.Add(annotation);

    public bool Undo()
    {
        if (_annotations.Count == 0)
        {
            return false;
        }

        _annotations.RemoveAt(_annotations.Count - 1);
        return true;
    }

    /// <summary>
    /// A frozen copy for the render thread. Avalonia runs ICustomDrawOperation
    /// off the UI thread, so iterating the live list races with editing.
    /// </summary>
    public IReadOnlyList<IAnnotation> SnapshotAnnotations() => _annotations.ToArray();

    public void Dispose() => Source.Dispose();
}
