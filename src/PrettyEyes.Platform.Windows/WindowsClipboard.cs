using System.Diagnostics;
using System.Runtime.InteropServices;
using PrettyEyes.Core.Diagnostics;
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

    /// <summary>
    /// How hard zlib works on the PNG. Measured on a 2440x1319 game frame:
    /// level 6, which is what Skia picks when nobody says otherwise, costs
    /// 1200 ms and produces 4.4 MB; level 1 costs 204 ms and produces 5.0 MB.
    /// The clipboard keeps its bytes in memory until the next copy, so 13% of
    /// size buys back four fifths of the wait. The quality argument of
    /// SKImage.Encode does not touch this: 80 and 100 produce byte-identical
    /// output in the same time, because PNG is lossless and Skia reads that
    /// number for the formats where it means something.
    /// </summary>
    private const int ZLibLevel = 1;

    /// <summary>Alpha at full strength, in a BGRA word.</summary>
    private const uint Opaque = 0xFF000000;

    public async Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken)
    {
        byte[] dib;
        byte[] png;

        // Every stage below runs on whatever thread called in. Which thread
        // that is decides whether the overlay is still answering the mouse
        // while a 2K picture is encoded, so it is written down with the rest.
        var thread = Environment.CurrentManagedThreadId;
        var stage = Stopwatch.GetTimestamp();
        double read;
        double flatten;
        double bitmap;
        double encode;

        try
        {
            // Two formats, two answers to transparency. PNG carries the alpha
            // exactly as it was drawn, so an application that understands PNG
            // gets the transparent export it was promised. The DIB cannot carry
            // it at all, so a picture that has transparency in it is laid on
            // white first: without that, the soft shadow around a transparent
            // export pastes as a black smear, which is the same bug we already
            // fixed once for Photoshop.
            //
            // Whether there is any transparency to answer for is not known
            // until the pixels are read, and they have to be read for the
            // bitmap anyway. So the bitmap is built first and says whether it
            // met a pixel that was not fully opaque; only then is the white
            // laid down, and only then are the pixels read a second time.
            // Without decoration - which is the ordinary case - that saves a
            // whole surface and a second pass over several megabytes.
            var pixels = Read(image);
            read = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;

            stage = Stopwatch.GetTimestamp();
            dib = BuildDib(pixels, out var transparent);
            bitmap = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;

            flatten = 0;

            if (transparent)
            {
                stage = Stopwatch.GetTimestamp();

                using var flattened = DocumentRenderer.Composite(image, SKColors.White);

                dib = BuildDib(Read(flattened), out _);
                flatten = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
            }

            stage = Stopwatch.GetTimestamp();
            png = EncodePng(image);
            encode = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        }
        catch (Exception error) when (error is InvalidOperationException or OutOfMemoryException)
        {
            return SinkResult.Failed;
        }

        stage = Stopwatch.GetTimestamp();

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (Write(dib, png))
            {
                Report(image, thread, read, flatten, bitmap, encode, stage, attempt);

                return SinkResult.Sent;
            }

            if (attempt == Attempts)
            {
                Report(image, thread, read, flatten, bitmap, encode, stage, attempt);

                return SinkResult.Failed;
            }

            await Task.Delay(RetryDelayMs, cancellationToken);
        }

        return SinkResult.Failed;
    }

    /// <summary>
    /// One line per copy: where the time between the key and an empty screen
    /// actually goes. Written after the handover rather than around it so a
    /// retry loop shows up as its own number instead of hiding in the total.
    /// </summary>
    private static void Report(
        SKImage image,
        int thread,
        double read,
        double flatten,
        double bitmap,
        double encode,
        long handoverFrom,
        int attempts)
    {
        var handover = Stopwatch.GetElapsedTime(handoverFrom).TotalMilliseconds;

        Log.Default.Info(
            $"буфер {image.Width}x{image.Height}: чтение {read:F1}, dib {bitmap:F1}, подложка {flatten:F1}, " +
            $"png {encode:F1}, отдача {handover:F1} мс, попыток {attempts}, поток {thread}");
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
    /// The picture's pixels, as BGRA with the alpha left exactly as it is.
    /// </summary>
    private static (byte[] Pixels, SKImageInfo Info) Read(SKImage image)
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

        return (pixels, info);
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
    /// <param name="transparent">
    /// Whether any pixel arrived less than fully opaque, which is the caller's
    /// cue to lay the picture on white and build the bitmap again. Answered
    /// here because this is the pass that already touches every pixel.
    /// </param>
    private static byte[] BuildDib((byte[] Pixels, SKImageInfo Info) source, out bool transparent)
    {
        var (pixels, info) = source;
        var stride = info.Width * 4;

        const int HeaderSize = 40;
        var dib = new byte[HeaderSize + pixels.Length];

        WriteInt(dib, 0, HeaderSize);
        WriteInt(dib, 4, info.Width);
        WriteInt(dib, 8, info.Height);          // positive: rows run bottom to top
        WriteShort(dib, 12, 1);                 // planes
        WriteShort(dib, 14, 32);                // bits per pixel
        WriteInt(dib, 16, 0);                   // BI_RGB
        WriteInt(dib, 20, pixels.Length);

        // A word at a time rather than a byte at a time, because the same pass
        // has to answer whether anything was transparent and a word compares
        // in one go. Not for speed: measured on 12.3 MB, byte and word passes
        // both cost about 2 ms over the 1 ms the copying alone takes, so this
        // is bound by memory rather than by the number of iterations. What the
        // stage does cost - some 20 ms - is the 13 MB the bitmap is built in.
        var seen = Opaque;

        for (var y = 0; y < info.Height; y++)
        {
            var target = HeaderSize + ((info.Height - 1 - y) * stride);

            Buffer.BlockCopy(pixels, y * stride, dib, target, stride);

            var row = MemoryMarshal.Cast<byte, uint>(dib.AsSpan(target, stride));

            for (var x = 0; x < row.Length; x++)
            {
                var pixel = row[x];

                seen &= pixel;
                row[x] = pixel | Opaque;
            }
        }

        transparent = seen != Opaque;

        return dib;
    }

    /// <summary>
    /// The PNG, compressed as lightly as the clipboard deserves.
    ///
    /// Encoded off the pixmap rather than through SKImage.Encode because that
    /// overload takes a quality number PNG has no use for and gives no way to
    /// say how hard to compress, which is the only thing that matters here.
    /// The fallback is that same overload: an image that will not hand over a
    /// pixmap is rare enough to be worth a slow path and a line in the log
    /// rather than a failed copy.
    /// </summary>
    private static byte[] EncodePng(SKImage image)
    {
        using var pixmap = image.PeekPixels();

        if (pixmap is not null)
        {
            using var stream = new MemoryStream();

            if (pixmap.Encode(stream, new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, ZLibLevel)))
            {
                return stream.ToArray();
            }
        }

        Log.Default.Info("снимок не отдал пиксели напрямую, PNG кодируется медленным путём");

        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
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
