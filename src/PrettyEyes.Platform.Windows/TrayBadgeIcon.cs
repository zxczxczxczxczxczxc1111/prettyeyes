using System.Runtime.InteropServices;
using PrettyEyes.Core.Updates;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// A second copy of the tray icon with the update mark stamped into its
/// corner.
///
/// Composed once and kept, because the alternative is building an icon inside
/// a message handler. Where the mark goes is decided in Core by
/// <see cref="BadgePainter"/>; everything here is the Windows tax of turning
/// an HICON into pixels and back.
/// </summary>
internal static class TrayBadgeIcon
{
    /// <summary>
    /// Returns zero when the icon cannot be taken apart - an icon with no
    /// colour bitmap, or one from before alpha channels. The caller keeps
    /// showing the plain icon, which is a missing mark rather than a black
    /// square where the icon used to be.
    /// </summary>
    internal static IntPtr Compose(IntPtr icon, int side)
    {
        if (icon == IntPtr.Zero || side <= 0)
        {
            return IntPtr.Zero;
        }

        if (!NativeMethods.GetIconInfo(icon, out var parts))
        {
            return IntPtr.Zero;
        }

        try
        {
            var pixels = ReadPixels(parts.hbmColor, side);

            if (pixels is null)
            {
                return IntPtr.Zero;
            }

            BadgePainter.Stamp(pixels, side);

            return BuildIcon(pixels, side, parts.xHotspot, parts.yHotspot);
        }
        finally
        {
            // Both bitmaps come back owned by us whether we asked for them or
            // not. This is the usual way this call leaks.
            Delete(parts.hbmColor);
            Delete(parts.hbmMask);
        }
    }

    private static byte[]? ReadPixels(IntPtr bitmap, int side)
    {
        if (bitmap == IntPtr.Zero)
        {
            return null;
        }

        var pixels = new byte[side * side * 4];
        var header = Header(side);
        var screen = NativeMethods.GetDC(IntPtr.Zero);

        try
        {
            // Asking for 32 bits top-down converts whatever the icon actually
            // is, so the depth of the source never has to be worked out.
            if (NativeMethods.GetDIBits(
                    screen, bitmap, 0, (uint)side, pixels, ref header, NativeMethods.DIB_RGB_COLORS) == 0)
            {
                return null;
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }

        // Alpha of zero everywhere means the icon carries its transparency in
        // a mask instead, and the mask is not read here. Handing that back
        // would produce an icon that is entirely see-through.
        for (var offset = 3; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset] != 0)
            {
                return pixels;
            }
        }

        return null;
    }

    private static IntPtr BuildIcon(byte[] pixels, int side, int xHotspot, int yHotspot)
    {
        var header = Header(side);
        var screen = NativeMethods.GetDC(IntPtr.Zero);
        var colour = IntPtr.Zero;
        var mask = IntPtr.Zero;

        try
        {
            colour = NativeMethods.CreateDIBSection(
                screen, ref header, NativeMethods.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);

            if (colour == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            Marshal.Copy(pixels, 0, bits, pixels.Length);

            // Wherever the colour bitmap carries alpha the mask is ignored, but
            // CreateIconIndirect still wants one and an uninitialised one shows
            // through in the code paths that do not read alpha. Zero means
            // "keep the colour".
            mask = NativeMethods.CreateBitmap(side, side, 1, 1, new byte[(side + 31) / 32 * 4 * side]);

            if (mask == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var parts = new NativeMethods.IconInfo
            {
                fIcon = true,
                xHotspot = xHotspot,
                yHotspot = yHotspot,
                hbmColor = colour,
                hbmMask = mask,
            };

            // Copies both bitmaps, so ours are deleted on the way out.
            return NativeMethods.CreateIconIndirect(ref parts);
        }
        finally
        {
            Delete(colour);
            Delete(mask);
            NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>A negative height means the top row comes first, which is how BadgePainter reads it.</summary>
    private static NativeMethods.BitmapInfo Header(int side) => new()
    {
        bmiHeader = new NativeMethods.BitmapInfoHeader
        {
            biSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            biWidth = side,
            biHeight = -side,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        },
    };

    private static void Delete(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.DeleteObject(handle);
        }
    }
}
