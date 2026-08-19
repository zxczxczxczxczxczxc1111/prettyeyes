using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Turns a document into a flat image. The single path used by both
/// "copy to clipboard" and "save to file", so effects are always baked in.
/// </summary>
public static class DocumentRenderer
{
    public static SKImage Render(Document document)
    {
        var frame = document.SourceBounds;

        var selection = document.Selection.IsEmpty
            ? frame
            : document.Selection.Intersect(frame);

        if (selection.IsEmpty)
        {
            throw new InvalidOperationException("Selection lies entirely outside the captured frame.");
        }

        var info = new SKImageInfo(selection.Width, selection.Height);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {selection.Width}x{selection.Height} surface.");

        var canvas = surface.Canvas;

        // Work in virtual-desktop coordinates, then crop by translating.
        canvas.Translate(-selection.X, -selection.Y);
        canvas.DrawImage(document.Source, frame.X, frame.Y);

        foreach (var annotation in document.SnapshotAnnotations())
        {
            annotation.Draw(canvas, document.Source, frame);
        }

        return surface.Snapshot();
    }
}
