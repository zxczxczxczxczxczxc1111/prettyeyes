using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// Hides a region by blurring it. The radius comes from the region size and is
/// deliberately strong: weak blur is reversible, and this tool exists to hide
/// data.
/// </summary>
public sealed class BlurAnnotation : IAnnotation
{
    /// <summary>Fraction of the shorter side used as blur sigma.</summary>
    private const float SigmaRatio = 0.12f;

    /// <summary>Below this, small regions stay readable.</summary>
    private const float MinSigma = 6f;

    /// <summary>
    /// Extra pixels sampled around the region so the blur has real content to
    /// pull from at the edges instead of fading into nothing.
    /// </summary>
    private const float PaddingSigmas = 3f;

    public BlurAnnotation(CaptureRect bounds) => Bounds = bounds;

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
    {
        if (Bounds.IsEmpty)
        {
            return;
        }

        var sigma = Math.Max(MinSigma, Math.Min(Bounds.Width, Bounds.Height) * SigmaRatio);
        var padding = (int)Math.Ceiling(sigma * PaddingSigmas);

        // Region to sample, in image coordinates, clamped to the image.
        var imageBounds = new CaptureRect(0, 0, source.Width, source.Height);
        var padded = new CaptureRect(
                Bounds.X - sourceOrigin.X - padding,
                Bounds.Y - sourceOrigin.Y - padding,
                Bounds.Width + padding * 2,
                Bounds.Height + padding * 2)
            .Intersect(imageBounds);

        if (padded.IsEmpty)
        {
            return;
        }

        // Computed once per region and kept: the blur tool builds a new
        // annotation on every pointer move, so a cache inside this object would
        // never be hit. Measured cost of a miss: about 1.5 ms.
        var blurred = BlurCache.Shared.Get(source, padded, sigma, () => Build(source, padded, sigma));

        var clip = SKRect.Create(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);

        canvas.Save();
        canvas.ClipRect(clip);
        canvas.DrawImage(blurred, padded.X + sourceOrigin.X, padded.Y + sourceOrigin.Y);
        canvas.Restore();
    }

    /// <summary>
    /// Blurs one slice of the capture. Filtering the whole desktop image would
    /// be what makes naive implementations crawl.
    /// </summary>
    private static SKImage Build(SKImage source, CaptureRect padded, float sigma)
    {
        using var slice = source.Subset(SKRectI.Create(padded.X, padded.Y, padded.Width, padded.Height))
            ?? throw new InvalidOperationException("Could not take the region out of the capture.");

        using var blur = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { ImageFilter = blur };
        using var surface = SKSurface.Create(new SKImageInfo(padded.Width, padded.Height))
            ?? throw new InvalidOperationException($"Could not allocate a {padded.Width}x{padded.Height} surface.");

        surface.Canvas.DrawImage(slice, 0, 0, paint);

        return surface.Snapshot();
    }
}
