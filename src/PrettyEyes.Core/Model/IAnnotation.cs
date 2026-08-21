using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Core.Model;

/// <summary>
/// One drawn object on the screenshot.
/// </summary>
public interface IAnnotation
{
    CaptureRect Bounds { get; }

    /// <summary>
    /// Draws itself onto a canvas whose origin is the virtual desktop origin.
    /// </summary>
    /// <param name="source">
    /// The untouched capture. Effects that sample pixels (blur, mosaic) must
    /// read from it, never from the canvas, so overlapping effects do not stack.
    /// </param>
    /// <param name="sourceOrigin">
    /// Where the source image sits in virtual-desktop coordinates. Its X and Y
    /// are negative whenever a monitor is placed left of or above the primary
    /// one, and sampling code has to subtract them to hit the right pixels.
    /// </param>
    /// <param name="cache">
    /// Where expensive sampled effects keep their result. Handed in rather than
    /// reached for: the cache belongs to one document, and an annotation has no
    /// way to know which document is drawing it.
    /// </param>
    void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin, BlurCache cache);
}
