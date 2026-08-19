using SkiaSharp;

namespace PrettyEyes.Core.Platform;

public interface IImageSink
{
    Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken);
}
