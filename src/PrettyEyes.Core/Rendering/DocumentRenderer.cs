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
    /// <summary>How soft the drop shadow is, relative to the padding.</summary>
    private const float ShadowBlur = 0.5f;

    private const float ShadowOffset = 0.25f;

    public static SKImage Render(Document document) => Render(document, ExportStyle.None);

    public static SKImage Render(Document document, ExportStyle style)
    {
        var frame = document.SourceBounds;

        var selection = document.Selection.IsEmpty
            ? frame
            : document.Selection.Intersect(frame);

        if (selection.IsEmpty)
        {
            throw new InvalidOperationException("Selection lies entirely outside the captured frame.");
        }

        var shot = Flatten(document, frame, selection);
        var fitted = style.FitTo(selection.Width, selection.Height);

        // Without decoration the caller owns the screenshot as it is.
        if (!fitted.Enabled)
        {
            return shot;
        }

        // With decoration it is an intermediate, and it dies here.
        using (shot)
        {
            return Decorate(shot, fitted);
        }
    }

    /// <summary>The screenshot itself: the captured pixels plus what was drawn on them.</summary>
    private static SKImage Flatten(Document document, CaptureRect frame, CaptureRect selection)
    {
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

    /// <summary>Padding, backdrop, rounded corners and a shadow, in that order.</summary>
    private static SKImage Decorate(SKImage shot, ExportStyle style)
    {
        var width = shot.Width + (style.Padding * 2);
        var height = shot.Height + (style.Padding * 2);

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {width}x{height} surface.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, style.Background, width, height);

        var destination = SKRect.Create(style.Padding, style.Padding, shot.Width, shot.Height);
        using var paint = new SKPaint { IsAntialias = true };

        if (style.Shadow)
        {
            paint.ImageFilter = SKImageFilter.CreateDropShadow(
                0,
                style.Padding * ShadowOffset,
                style.Padding * ShadowBlur,
                style.Padding * ShadowBlur,
                new SKColor(0, 0, 0, 140));
        }

        if (style.CornerRadius > 0)
        {
            // Rounded through a layer so the shadow follows the rounded shape
            // instead of the square one underneath it.
            using var rounded = new SKRoundRect(destination, style.CornerRadius);

            canvas.SaveLayer(paint);
            canvas.ClipRoundRect(rounded, antialias: true);
            canvas.DrawImage(shot, destination);
            canvas.Restore();
        }
        else
        {
            canvas.DrawImage(shot, destination, new SKSamplingOptions(SKFilterMode.Linear), paint);
        }

        return surface.Snapshot();
    }

    private static void DrawBackground(SKCanvas canvas, ExportBackground background, int width, int height)
    {
        var area = SKRect.Create(0, 0, width, height);

        switch (background)
        {
            case ExportBackground.Transparent:
                return;

            case ExportBackground.White:
                canvas.Clear(SKColors.White);
                return;

            case ExportBackground.Gradient:
                // Two greys from the same family as the interface: a backdrop
                // that says "this is a screenshot" without competing with it.
                using (var shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(width, height),
                    [new SKColor(0x1C, 0x1C, 0x20), new SKColor(0x0A, 0x0A, 0x0B)],
                    SKShaderTileMode.Clamp))
                {
                    using var paint = new SKPaint { Shader = shader };
                    canvas.DrawRect(area, paint);
                }

                return;

            default:
                canvas.Clear(SKColors.Black);
                return;
        }
    }
}
