using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Settings;
using SkiaSharp;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Writes the screenshot straight into a folder, no dialog, no notification.
///
/// A thin wrapper on purpose: the name template and the free-name search live
/// in Core where they are tested, and what is left here is the part that
/// touches a disk.
/// </summary>
public sealed class FolderSink : IImageSink
{
    /// <summary>
    /// A network drive that has gone away does not refuse, it hangs. Three
    /// seconds is long past any local write and short enough to be a hiccup.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly Func<SaveOptions> _options;
    private readonly Func<DateTimeOffset> _now;

    public FolderSink(Func<SaveOptions> options, Func<DateTimeOffset> now)
    {
        _options = options;
        _now = now;
    }

    /// <summary>Where the last screenshot went, for "open the folder".</summary>
    public string? LastFolder { get; private set; }

    public async Task<SinkResult> SendAsync(SKImage image, CancellationToken cancellationToken)
    {
        var options = _options();

        if (!options.Ready)
        {
            return SinkResult.Failed;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);

        try
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();

            var path = await Task.Run(
                () =>
                {
                    Directory.CreateDirectory(options.Folder);

                    var wanted = FileNameTemplate.Format(options.Template, _now());
                    var name = UniqueName.For(wanted, candidate => File.Exists(Path.Combine(options.Folder, candidate)));
                    var full = Path.Combine(options.Folder, name);

                    File.WriteAllBytes(full, bytes);

                    return full;
                },
                deadline.Token);

            LastFolder = options.Folder;

            return SinkResult.Sent;
        }
        catch (OperationCanceledException)
        {
            // A drive that stopped answering, or the caller gave up.
            return SinkResult.Failed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SinkResult.Failed;
        }
    }

    /// <summary>
    /// Whether the folder can actually be written to, checked when it is picked
    /// rather than when a screenshot is waiting.
    /// </summary>
    public static bool CanWrite(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var probe = Path.Combine(folder, $".prettyeyes-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
