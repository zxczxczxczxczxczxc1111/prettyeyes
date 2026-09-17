using Avalonia.Platform.Storage;
using PrettyEyes.Core.Platform;
using SkiaSharp;

namespace PrettyEyes.App.Services;

public sealed class FileSink : IImageSink
{
    private readonly IStorageProvider _storage;
    private readonly Func<DateTimeOffset> _now;

    public FileSink(IStorageProvider storage, Func<DateTimeOffset> now)
    {
        _storage = storage;
        _now = now;
    }

    public async Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken)
    {
        var file = await _storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = $"prettyeyes-{_now():yyyy-MM-dd-HH-mm-ss}.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });

        if (file is null)
        {
            return SinkResult.Cancelled;
        }

        try
        {
            // Off the UI thread: the overlay is still on screen behind the
            // dialog, and a full monitor of a detailed picture takes about a
            // second to compress. Left here, that second is spent with the
            // message loop stopped and the overlay unable to repaint.
            using var data = await Task.Run(() => image.Encode(SKEncodedImageFormat.Png, 100), cancellationToken);
            await using var target = await file.OpenWriteAsync();
            data.SaveTo(target);

            return SinkResult.Sent;
        }
        catch (IOException)
        {
            // Disk full, path gone, file locked by another process.
            return SinkResult.Failed;
        }
        catch (UnauthorizedAccessException)
        {
            return SinkResult.Failed;
        }
    }
}
