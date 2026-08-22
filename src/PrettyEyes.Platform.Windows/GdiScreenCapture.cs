using System.Runtime.InteropServices;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Paints a monitor with GDI BitBlt.
///
/// Last in the chain and kept for exactly that: it needs nothing at all from
/// the system, so it works where Desktop Duplication is refused and where
/// Windows.Graphics.Capture does not exist. Known limitation, and the reason
/// it is last: hardware-accelerated and DRM-protected windows come out black.
///
/// Costs nothing while it sits unused - there is no device, no session, no
/// thread here, only a device context borrowed for the length of one monitor.
/// </summary>
public sealed class GdiScreenCapture : IMonitorPainter
{
    public string Name => "GDI";

    public void Paint(MonitorInfo monitor, IntPtr destination, int stride)
    {
        var bounds = monitor.Bounds;

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
                    $"BitBlt failed while capturing {monitor.DeviceId} (win32 error {Marshal.GetLastWin32Error()}).");
            }

            NativeMethods.SelectObject(memoryDc, previous);

            Pour(memoryDc, bitmap, bounds, destination, stride);
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

    /// <summary>
    /// Nothing to release: this painter owns no device, no thread and no
    /// session. Present because the chain disposes everyone the same way.
    /// </summary>
    public void Dispose()
    {
    }

    private static void Pour(IntPtr dc, IntPtr bitmap, CaptureRect bounds, IntPtr destination, int stride)
    {
        var info = new NativeMethods.BitmapInfo();
        info.bmiHeader.biSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>();
        info.bmiHeader.biWidth = bounds.Width;
        // Negative height gives a top-down bitmap, matching Skia's row order.
        info.bmiHeader.biHeight = -bounds.Height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0;

        var buffer = new byte[bounds.Width * bounds.Height * 4];
        var lines = NativeMethods.GetDIBits(dc, bitmap, 0, (uint)bounds.Height, buffer, ref info, 0);

        if (lines == 0)
        {
            throw new InvalidOperationException("GetDIBits returned no scan lines.");
        }

        // GDI leaves the alpha byte unset; force opaque or Skia renders nothing.
        for (var i = 3; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;
        }

        // GetDIBits packs its rows tightly, the desktop buffer does not: a
        // monitor sits inside a wider picture and every row lands stride bytes
        // further on.
        var rowBytes = bounds.Width * 4;

        for (var row = 0; row < bounds.Height; row++)
        {
            Marshal.Copy(buffer, row * rowBytes, destination + (row * (nint)stride), rowBytes);
        }
    }
}
