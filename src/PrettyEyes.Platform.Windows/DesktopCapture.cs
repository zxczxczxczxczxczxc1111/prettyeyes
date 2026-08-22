using System.Diagnostics;
using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
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
    private string _said = string.Empty;
    private int _taken;

    public DesktopCapture(
        IMonitorEnumerator monitors,
        IReadOnlyList<IMonitorPainter> painters,
        Action<string, double>? timing = null)
    {
        _monitors = monitors;
        _chain = new PainterChain(painters);
        _timing = timing;
    }

    /// <summary>
    /// Which engine painted which monitor last time, and how long the last
    /// capture took. Shown in the settings window: a monitor quietly handed to
    /// the older engine looks exactly like a working application until
    /// somebody notices the yellow border is back.
    /// </summary>
    public IReadOnlyDictionary<string, string> Painters => _chain.Assignments;

    public double LastMilliseconds { get; private set; }

    public CaptureResult CaptureAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var whole = Stopwatch.StartNew();
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

        Announce(layout);

        var info = new SKImageInfo(frame.Bounds.Width, frame.Bounds.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // FromPixels over our own buffer, not FromPixelCopy: Skia hands the
        // memory back through the release callback when the image dies. That is
        // one 28 MB copy less on every capture.
        using var pixmap = new SKPixmap(info, buffer, frame.Stride);

        var image = Step(
            "image",
            () => SKImage.FromPixels(pixmap, (address, _) => _buffers.Return(address, frame.Size), null))
            ?? throw new InvalidOperationException("Skia rejected the captured pixel buffer.");

        whole.Stop();

        // The first capture of a run is the warm-up: the code is being
        // compiled, the devices woken, the outputs found, and it costs seven
        // times what the ones after it cost. Showing that number as "the last
        // screenshot" would tell the user the application is slow when it is
        // not.
        if (_taken++ > 0)
        {
            LastMilliseconds = whole.Elapsed.TotalMilliseconds;
        }

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
    /// Says who painted what, and says it again whenever that changes.
    ///
    /// Once a run is not enough, and that was learned rather than guessed: a
    /// monitor unplugged and plugged back in can quietly move to the older
    /// engine, which looks exactly like a working application right up until
    /// somebody notices the yellow border is back.
    /// </summary>
    private void Announce(DesktopLayout layout)
    {
        _chain.KeepOnly(layout.Monitors.Select(monitor => monitor.DeviceId));

        var now = string.Join(", ", _chain.Assignments
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}: {pair.Value}"));

        if (now == _said)
        {
            return;
        }

        _said = now;
        Log.Default.Info("маляры: " + now);
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
