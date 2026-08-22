using System.Diagnostics;
using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using SkiaSharp;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// One screenshot is one buffer and one painter per monitor.
///
/// Engines are not chosen for the whole screenshot any more. A rotated output
/// or an adapter that refuses to duplicate takes its own monitor to the older
/// engine and leaves the rest alone: falling the whole capture back to
/// Windows.Graphics.Capture would bring the yellow border back on every monitor
/// in order to fix one.
/// </summary>
public sealed class DesktopCapture : IScreenCapture, IDisposable
{
    private readonly IMonitorEnumerator _monitors;
    private readonly PainterChain _chain;
    private readonly FrameBuffers _buffers = new();
    private readonly Action<string, double>? _timing;

    private bool _disposed;
    private bool _said;

    public DesktopCapture(
        IMonitorEnumerator monitors,
        IReadOnlyList<IMonitorPainter> painters,
        Action<string, double>? timing = null)
    {
        _monitors = monitors;
        _chain = new PainterChain(painters);
        _timing = timing;
    }

    public CaptureResult CaptureAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var layout = _monitors.Enumerate();
        var frame = DesktopFrameLayout.For(layout);

        var buffer = Step("alloc", () => _buffers.Rent(frame.Size, frame.NeedsZeroing));

        try
        {
            // In parallel because a monitor spends most of its capture waiting
            // for a frame, and those waits are the same whether they happen one
            // after another or at once. Painters are told in their own contract
            // that this happens.
            Parallel.ForEach(frame.Placements, placement =>
                _chain.Paint(placement.Monitor, buffer + (nint)placement.Offset, frame.Stride));
        }
        catch (AggregateException error)
        {
            _buffers.Return(buffer, frame.Size);

            // One monitor failing is the whole capture failing; the first
            // reason is the useful one.
            throw error.InnerException ?? error;
        }
        catch
        {
            _buffers.Return(buffer, frame.Size);

            throw;
        }

        Announce();

        var info = new SKImageInfo(frame.Bounds.Width, frame.Bounds.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // FromPixels over our own buffer, not FromPixelCopy: Skia hands the
        // memory back through the release callback when the image dies. That is
        // one 28 MB copy less on every capture.
        using var pixmap = new SKPixmap(info, buffer, frame.Stride);

        var image = Step(
            "image",
            () => SKImage.FromPixels(pixmap, (address, _) => _buffers.Return(address, frame.Size), null))
            ?? throw new InvalidOperationException("Skia rejected the captured pixel buffer.");

        return new CaptureResult(image, layout);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _chain.Dispose();
        _buffers.Dispose();
    }

    /// <summary>
    /// Says once who painted what.
    ///
    /// Worth a line because it is the first question to ask when somebody
    /// reports the yellow border is back: a monitor quietly handed to the older
    /// engine looks exactly like a working screenshot until you look at the
    /// edge of the screen.
    /// </summary>
    private void Announce()
    {
        if (_said)
        {
            return;
        }

        _said = true;
        Log.Default.Info("маляры: " + string.Join(", ", _chain.Assignments.Select(pair => $"{pair.Key}: {pair.Value}")));
    }

    private T Step<T>(string name, Func<T> body)
    {
        if (_timing is null)
        {
            return body();
        }

        var watch = Stopwatch.StartNew();
        var result = body();
        watch.Stop();
        _timing(name, watch.Elapsed.TotalMilliseconds);

        return result;
    }
}
