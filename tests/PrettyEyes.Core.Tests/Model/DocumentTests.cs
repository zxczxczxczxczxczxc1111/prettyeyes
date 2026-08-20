using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Model;

public class DocumentTests
{
    private sealed class FakeAnnotation : IAnnotation
    {
        public CaptureRect Bounds => new(0, 0, 10, 10);

        public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
        {
        }
    }

    private static Document NewDocument()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.Black);

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, 100, 100))
        {
            Selection = new CaptureRect(10, 10, 50, 50),
        };
    }

    [Fact]
    public void New_document_has_no_annotations()
    {
        using var document = NewDocument();

        Assert.Empty(document.Annotations);
    }

    [Fact]
    public void Add_appends_annotation()
    {
        using var document = NewDocument();

        document.Add(new FakeAnnotation());

        Assert.Single(document.Annotations);
    }

    [Fact]
    public void Undo_removes_last_annotation_and_reports_success()
    {
        using var document = NewDocument();
        document.Add(new FakeAnnotation());
        document.Add(new FakeAnnotation());

        Assert.True(document.Undo());
        Assert.Single(document.Annotations);
    }

    [Fact]
    public void Clear_removes_every_annotation()
    {
        using var document = NewDocument();
        document.Add(new FakeAnnotation());
        document.Add(new FakeAnnotation());

        document.Clear();

        Assert.Empty(document.Annotations);
    }

    [Fact]
    public void Undo_on_empty_document_returns_false()
    {
        using var document = NewDocument();

        Assert.False(document.Undo());
    }

    [Fact]
    public void Snapshot_does_not_change_when_the_document_does()
    {
        using var document = NewDocument();
        document.Add(new FakeAnnotation());

        var snapshot = document.SnapshotAnnotations();
        document.Add(new FakeAnnotation());

        // The render thread iterates the snapshot while the UI thread edits.
        Assert.Single(snapshot);
        Assert.Equal(2, document.Annotations.Count);
    }

    [Fact]
    public void Dispose_releases_the_source_image()
    {
        var document = NewDocument();
        Assert.NotEqual(IntPtr.Zero, document.Source.Handle);

        document.Dispose();

        // SkiaSharp zeroes the native handle on dispose. Touching a disposed
        // SKImage crashes the process instead of throwing, so the handle is the
        // only safe thing to assert on.
        Assert.Equal(IntPtr.Zero, document.Source.Handle);
    }

    [Fact]
    public void Snapshot_is_reused_until_the_list_changes()
    {
        using var document = NewDocument();
        document.Add(new FakeAnnotation());

        var first = document.SnapshotAnnotations();
        var again = document.SnapshotAnnotations();

        // Same instance: the overlay asks for this on every rendered frame.
        Assert.Same(first, again);

        document.Add(new FakeAnnotation());

        Assert.NotSame(first, document.SnapshotAnnotations());
    }

    [Fact]
    public void Undo_and_clear_invalidate_the_snapshot_too()
    {
        using var document = NewDocument();
        document.Add(new FakeAnnotation());

        var afterAdd = document.SnapshotAnnotations();
        document.Undo();
        var afterUndo = document.SnapshotAnnotations();

        Assert.NotSame(afterAdd, afterUndo);

        document.Add(new FakeAnnotation());
        var afterSecondAdd = document.SnapshotAnnotations();
        document.Clear();

        Assert.NotSame(afterSecondAdd, document.SnapshotAnnotations());
    }
}
