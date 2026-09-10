using System.Runtime.InteropServices;
using SkiaSharp;

namespace PrettyEyes.Core.Updates;

/// <summary>
/// The dot the tray icon wears while an update waits.
///
/// Takes the icon's pixels rather than an icon: composing HICONs is Windows
/// work and belongs in the platform layer, but where the dot goes and what it
/// looks like is a decision, and decisions are testable.
///
/// Pixels are BGRA with premultiplied alpha, top row first - the layout of a
/// 32-bit DIB section, which is what the icon is built from on the other side.
/// </summary>
public static class BadgePainter
{
    /// <summary>
    /// The accent, the same violet the settings window uses for a live toggle.
    /// Green and amber are spoken for by status, and a waiting release is not
    /// a status.
    /// </summary>
    public const uint Fill = 0xFF9855E0;

    /// <summary>
    /// Nearly black, and not the accent darkened: the outline has to hold the
    /// dot together against a light taskbar as well as a dark one.
    /// </summary>
    private const uint Ring = 0xFF0F0F12;

    public static void Stamp(byte[] pixels, int side)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(side, 1);

        if (pixels.Length < side * side * 4)
        {
            throw new ArgumentException($"{side}x{side} needs {side * side * 4} bytes, got {pixels.Length}.", nameof(pixels));
        }

        // Three eighths of the side across. Five sixteenths, which the design
        // asked for, leaves three violet pixels inside the outline on a
        // 16-point tray: present in a screenshot, invisible on a taskbar.
        var outer = side * 3 / 16.0;

        // Snapped to a pixel centre so the middle of the dot lands on one
        // whole pixel instead of being split four ways, which at this size is
        // the difference between a dot and a smudge.
        var centre = Math.Floor(side - outer - 0.5) + 0.5;

        // One pixel of outline at 16, two at 32. A fixed pixel would be half
        // the dot on a small tray and a hairline on a large one.
        var band = Math.Max(1.0, side / 16.0);

        var info = new SKImageInfo(side, side, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            using var surface = SKSurface.Create(info, pinned.AddrOfPinnedObject(), side * 4);

            if (surface is null)
            {
                return;
            }

            using var outline = new SKPaint { Color = Ring, IsAntialias = true };
            using var body = new SKPaint { Color = Fill, IsAntialias = true };

            surface.Canvas.DrawCircle((float)centre, (float)centre, (float)outer, outline);
            surface.Canvas.DrawCircle((float)centre, (float)centre, (float)(outer - band), body);
            surface.Canvas.Flush();
        }
        finally
        {
            pinned.Free();
        }
    }
}
