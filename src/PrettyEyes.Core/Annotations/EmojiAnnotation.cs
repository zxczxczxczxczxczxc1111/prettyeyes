using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// A glyph stamped onto the screenshot.
///
/// The image belongs to whoever loaded it, not to this: the same glyph gets
/// stamped many times, and an annotation that disposed it would take the rest
/// with it.
/// </summary>
public sealed class EmojiAnnotation : IMovable
{
    private readonly SKImage _glyph;

    public EmojiAnnotation(CaptureRect bounds, SKImage glyph)
    {
        Bounds = bounds;
        _glyph = glyph;
    }

    public CaptureRect Bounds { get; }

    /// <summary>
    /// How much one turn of the wheel is worth. Eight pixels is a step you can
    /// see without being a step you overshoot.
    /// </summary>
    public const int SizeStep = 8;

    public IMovable MovedBy(int dx, int dy) =>
        new EmojiAnnotation(Bounds with { X = Bounds.X + dx, Y = Bounds.Y + dy }, _glyph);

    public IMovable? ResizedBy(int steps)
    {
        var size = Math.Clamp(Bounds.Width + (steps * SizeStep), EmojiTool.MinSize, EmojiTool.MaxSize);

        if (size == Bounds.Width)
        {
            return null;
        }

        // Around the middle: growing from a corner would walk the glyph away
        // from whatever it was put on.
        var shift = (size - Bounds.Width) / 2;

        return new EmojiAnnotation(
            new CaptureRect(Bounds.X - shift, Bounds.Y - shift, size, size),
            _glyph);
    }

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
