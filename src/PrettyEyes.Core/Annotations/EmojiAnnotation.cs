using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// A glyph stamped onto the screenshot.
///
/// The image belongs to whoever loaded it, not to this: the same glyph gets
/// stamped many times, and an annotation that disposed it would take the rest
/// with it.
/// </summary>
public sealed class EmojiAnnotation : IAnnotation
{
    private readonly SKImage _glyph;

    public EmojiAnnotation(CaptureRect bounds, SKImage glyph)
    {
        Bounds = bounds;
        _glyph = glyph;
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
    {
        if (Bounds.IsEmpty)
        {
            return;
        }

        var destination = SKRect.Create(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);

        // Linear: the glyphs are 72 pixels square and usually drawn larger, and
        // a nearest-neighbour emoji looks like a mistake rather than a choice.
        canvas.DrawImage(_glyph, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }
}
