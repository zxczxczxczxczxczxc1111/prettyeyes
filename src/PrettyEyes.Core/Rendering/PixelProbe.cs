using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Reads pixels off a frozen capture.
///
/// The map is peeked once and kept for as long as the capture lives. It used
/// to be peeked per question, and the questions are asked on every pointer
/// move: where the cursor is, what colour is under it, whether the picture
/// under it is light enough for dark ink.
///
/// Coordinates are virtual-desktop pixels, the same ones the annotations use.
/// Subtracting the frame origin is done here on purpose: every sampling bug in
/// this project so far has been a caller forgetting to do it, and the origin is
/// negative whenever a monitor sits left of or above the primary one.
/// </summary>
public sealed class PixelProbe : IDisposable
{
    /// <summary>
    /// Distance between samples of the luminance grid. Four is enough to tell
    /// a page from an editor and cheap enough to answer on every pointer move.
    /// </summary>
    private const int Step = 4;

    private readonly SKPixmap? _pixels;
    private readonly CaptureRect _bounds;

    public PixelProbe(SKImage source, CaptureRect bounds)
    {
        // Null for an image that has no raster pixels to hand over, which is
        // not something a capture produces but is something Skia is allowed to
        // answer. Every reading below then comes back null rather than throws.
        _pixels = source.PeekPixels();
        _bounds = bounds;
    }

    /// <summary>The colour of one pixel, or null when it is off the frame.</summary>
    public SKColor? ColourAt(int x, int y)
    {
        if (_pixels is null)
        {
            return null;
        }

        var px = x - _bounds.X;
        var py = y - _bounds.Y;

        return Inside(px, py) ? _pixels.GetPixelColor(px, py) : null;
    }

    /// <summary>
    /// How bright the picture is around a point, from 0 to 1, or null when
    /// nothing on the grid landed on the frame.
    ///
    /// Rec. 709 weights, which is what the crosshair has always used. The
    /// highlighter uses Rec. 601 with a threshold of its own and deliberately
    /// does not share this: the two thresholds were tuned against their own
    /// formulas, and one number for both would move both decisions.
    /// </summary>
    public double? LuminanceAround(int x, int y, int reach)
    {
        if (_pixels is null)
        {
            return null;
        }

        var total = 0.0;
        var counted = 0;

        for (var dy = -reach; dy <= reach; dy += Step)
        {
            for (var dx = -reach; dx <= reach; dx += Step)
            {
                var px = x + dx - _bounds.X;
                var py = y + dy - _bounds.Y;

                if (!Inside(px, py))
                {
                    // Skipped rather than counted as black: a cursor near the
                    // edge would otherwise be told the screen is darker than
                    // it is, and swap to the wrong ink.
                    continue;
                }

                var colour = _pixels.GetPixelColor(px, py);

                total += (0.2126 * colour.Red) + (0.7152 * colour.Green) + (0.0722 * colour.Blue);
                counted++;
            }
        }

        return counted == 0 ? null : total / (counted * 255.0);
    }

    private bool Inside(int px, int py) =>
        px >= 0 && py >= 0 && px < _pixels!.Width && py < _pixels.Height;

    public void Dispose() => _pixels?.Dispose();
}
