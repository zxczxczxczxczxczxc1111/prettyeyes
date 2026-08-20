using System.Runtime.InteropServices;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Puts a screenshot on the clipboard, both formats, by hand.
///
/// Avalonia's own SetBitmapAsync writes a device-independent bitmap that
/// Photoshop and anything else reading CF_DIB pastes as a black rectangle,
/// while the PNG alongside it is perfectly fine. Rather than guess at what its
/// header gets wrong, both formats are written here: the bitmap is what the
/// image editors take, the PNG is what browsers and chat clients take.
/// </summary>
public sealed class WindowsClipboard : IImageSink
{
    private const uint CfDib = 8;
    private const uint GmemMoveable = 0x0002;

    /// <summary>
    /// The clipboard is a single global lock. Another process holding it makes
    /// the call fail outright, and waiting a moment beats an error message.
    /// </summary>
    private const int Attempts = 5;

    private const int RetryDelayMs = 40;

    public async Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken)
    {
        byte[] dib;
        byte[] png;

        try
        {
            // Two formats, two answers to transparency. PNG carries the alpha
            // exactly as it was drawn, so an application that understands PNG
            // gets the transparent export it was promised. The DIB cannot carry
            // it at all, so the picture is laid on white first: without that,
            // the soft shadow around a transparent export pastes as a black
            // smear, which is the same bug we already fixed once for Photoshop.
            using var flattened = DocumentRenderer.Composite(image, SKColors.White);
            dib = BuildDib(flattened);

            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            png = encoded.ToArray();
        }
        catch (Exception error) when (error is InvalidOperationException or OutOfMemoryException)
        {
            return SinkResult.Failed;
        }

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (Write(dib, png))
            {
                return SinkResult.Sent;
            }

            if (attempt == Attempts)
            {
                return SinkResult.Failed;
            }

            await Task.Delay(RetryDelayMs, cancellationToken);
        }

        return SinkResult.Failed;
    }

    private static bool Write(byte[] dib, byte[] png)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            // The PNG format is registered by name; every application that
            // prefers lossless colour over a bitmap looks for exactly this one.
            var pngFormat = RegisterClipboardFormat("PNG");

            return Offer(CfDib, dib) && (pngFormat == 0 || Offer(pngFormat, png));
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// Hands one format to the clipboard. The memory belongs to the clipboard
    /// from the moment SetClipboardData succeeds and must not be freed here;
    /// if it fails, it is ours again and freeing it is the only way not to leak.
    /// </summary>
    private static bool Offer(uint format, byte[] bytes)
    {
        var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);

        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var target = GlobalLock(handle);

        if (target == IntPtr.Zero)
        {
            GlobalFree(handle);

            return false;
        }

        Marshal.Copy(bytes, 0, target, bytes.Length);
        GlobalUnlock(handle);

        if (SetClipboardData(format, handle) != IntPtr.Zero)
        {
            return true;
        }

        GlobalFree(handle);

        return false;
    }

    /// <summary>
    /// A 32-bit BI_RGB bitmap, bottom-up, with every alpha byte set to opaque.
    ///
    /// The alpha is the whole point. The captured frame carries whatever the
    /// desktop compositor left in that byte, which for ordinary opaque windows
    /// is zero; Skia is told the image is opaque and ignores it, so the PNG
    /// comes out right, but a bitmap handed to an application that does read
    /// alpha comes out as a black rectangle.
    /// </summary>
    private static byte[] BuildDib(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var stride = info.Width * 4;
        var pixels = new byte[stride * info.Height];

        var read = false;

        unsafe
        {
            fixed (byte* first = pixels)
            {
                read = image.ReadPixels(info, (IntPtr)first, stride, 0, 0);
            }
        }

        if (!read)
        {
            throw new InvalidOperationException("Не удалось прочитать пиксели снимка.");
        }

        const int HeaderSize = 40;
        var dib = new byte[HeaderSize + pixels.Length];

        WriteInt(dib, 0, HeaderSize);
        WriteInt(dib, 4, info.Width);
        WriteInt(dib, 8, info.Height);          // positive: rows run bottom to top
        WriteShort(dib, 12, 1);                 // planes
        WriteShort(dib, 14, 32);                // bits per pixel
        WriteInt(dib, 16, 0);                   // BI_RGB
        WriteInt(dib, 20, pixels.Length);

        for (var y = 0; y < info.Height; y++)
        {
            var source = y * stride;
            var target = HeaderSize + ((info.Height - 1 - y) * stride);

            Buffer.BlockCopy(pixels, source, dib, target, stride);

            for (var x = 3; x < stride; x += 4)
            {
                dib[target + x] = 255;
            }
        }

        return dib;
    }

    private static void WriteInt(byte[] buffer, int offset, int value) =>
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), value);

    private static void WriteShort(byte[] buffer, int offset, short value) =>
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 2), value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr handle);
}
