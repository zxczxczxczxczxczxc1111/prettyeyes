using System.Runtime.InteropServices;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;
using SkiaSharp;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Captures the virtual desktop with GDI BitBlt.
/// Known limitation: hardware-accelerated and DRM-protected windows come out
/// black. Replaced by a Windows.Graphics.Capture implementation in phase 15.
/// </summary>
public sealed class GdiScreenCapture : IScreenCapture
{
    private readonly IMonitorEnumerator _monitors;

    public GdiScreenCapture(IMonitorEnumerator monitors) => _monitors = monitors;

    public CaptureResult CaptureAll()
    {
        var layout = _monitors.Enumerate();
        var bounds = layout.VirtualBounds;

        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Virtual desktop reported a non-positive size.");
        }

        return new CaptureResult(Grab(bounds), layout);
    }

    private static SKImage Grab(CaptureRect bounds)
    {
        // Screen DC of the whole virtual desktop; understands negative origins.
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);

        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not obtain the screen device context.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);

            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not allocate a GDI capture buffer.");
            }

            var previous = NativeMethods.SelectObject(memoryDc, bitmap);

            // Plain SRCCOPY: CAPTUREBLT would also pull layered windows in, but
            // it makes the screen visibly flash and slows the blit down a lot.
            var copied = NativeMethods.BitBlt(
                memoryDc, 0, 0, bounds.Width, bounds.Height,
                screenDc, bounds.X, bounds.Y,
                NativeMethods.SRCCOPY);

            if (!copied)
            {
                throw new InvalidOperationException(
                    $"BitBlt failed while capturing the screen (win32 error {Marshal.GetLastWin32Error()}).");
            }

            NativeMethods.SelectObject(memoryDc, previous);

            return ToSkImage(memoryDc, bitmap, bounds.Width, bounds.Height);
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static SKImage ToSkImage(IntPtr dc, IntPtr bitmap, int width, int height)
    {
        var info = new NativeMethods.BitmapInfo();
        info.bmiHeader.biSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>();
        info.bmiHeader.biWidth = width;
        // Negative height gives a top-down bitmap, matching Skia's row order.
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0;

        var buffer = new byte[width * height * 4];
        var lines = NativeMethods.GetDIBits(dc, bitmap, 0, (uint)height, buffer, ref info, 0);

        if (lines == 0)
        {
            throw new InvalidOperationException("GetDIBits returned no scan lines.");
        }

        // GDI leaves the alpha byte unset; force opaque or Skia renders nothing.
        for (var i = 3; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;
        }

        var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // FromPixelCopy, not InstallPixels: the latter leaves the image pointing
        // at a managed array that the GC is free to move once unpinned.
        return SKImage.FromPixelCopy(imageInfo, buffer)
            ?? throw new InvalidOperationException("Skia rejected the captured pixel buffer.");
    }
}
