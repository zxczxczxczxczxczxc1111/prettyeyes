using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Platform;

/// <summary>
/// One frozen frame of the whole virtual desktop plus the layout it was taken
/// with. The image's top-left pixel corresponds to Bounds.X / Bounds.Y, which
/// are negative when a monitor sits left of or above the primary one.
///
/// Windows carries where the windows were at that moment. It has to travel
/// with the frame rather than be asked for later: once the overlay is up, the
/// window under the pointer is the overlay. Defaulted because an engine that
/// cannot answer is allowed to say so, and the gesture that reads it falls
/// back to the whole monitor.
/// </summary>
public sealed record CaptureResult(SKImage Image, DesktopLayout Layout, WindowShapes? Windows = null)
{
    public CaptureRect Bounds => Layout.VirtualBounds;

    public WindowShapes Shapes => Windows ?? WindowShapes.None;
}
