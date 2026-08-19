using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.Core.Platform;

/// <summary>
/// One frozen frame of the whole virtual desktop plus the layout it was taken
/// with. The image's top-left pixel corresponds to Bounds.X / Bounds.Y, which
/// are negative when a monitor sits left of or above the primary one.
/// </summary>
public sealed record CaptureResult(SKImage Image, DesktopLayout Layout)
{
    public CaptureRect Bounds => Layout.VirtualBounds;
}
