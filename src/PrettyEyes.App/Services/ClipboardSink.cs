using System.Runtime.InteropServices;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using PrettyEyes.Core.Platform;
using SkiaSharp;

namespace PrettyEyes.App.Services;

public sealed class ClipboardSink : IImageSink
{
    // The Windows clipboard is a global lock: another process holding it makes
    // the call fail outright, and retrying beats showing an error.
    private const int Attempts = 3;
    private const int RetryDelayMs = 50;

    private readonly IClipboard _clipboard;

    public ClipboardSink(IClipboard clipboard) => _clipboard = clipboard;

    public async Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var bitmap = new Bitmap(stream);

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                await _clipboard.SetBitmapAsync(bitmap);

                // Without this the clipboard only holds a promise served by
                // this process, and every consumer gets nothing back once the
                // bitmap is disposed. FlushAsync materialises the data.
                await _clipboard.FlushAsync();

                return SinkResult.Sent;
            }
            catch (ExternalException)
            {
                // Includes COMException: the clipboard was busy.
                if (attempt == Attempts)
                {
                    return SinkResult.Failed;
                }

                await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }

        return SinkResult.Failed;
    }
}
