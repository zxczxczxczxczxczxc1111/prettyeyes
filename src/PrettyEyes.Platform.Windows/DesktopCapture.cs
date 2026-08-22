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

    /// <summary>
    /// Three minutes. Long enough that a burst of screenshots never pays the
    /// rebuild, short enough that a tray application left alone gives the
    /// memory back while the person is still in the same meeting.
    /// </summary>
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(3);

    private readonly IdleWatch _watch = new(Idle);

    /// <summary>
    /// Held while a capture paints and while the engine is released. The timer
    /// that releases runs on its own thread, and letting go of a Direct3D
    /// device from under a capture in progress is a crash, not a saving.
    /// </summary>
    private readonly object _engine = new();

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
        _watch.Touch(DateTime.Now);

        var layout = _monitors.Enumerate();
        var frame = DesktopFrameLayout.For(layout);

        var buffer = Step("alloc", () => _buffers.Rent(frame.Size, frame.NeedsZeroing));

        try
        {
            // One monitor after another, not at once.
            //
            // Painting in parallel used to save about four milliseconds and
            // cost two Direct3D devices, seventeen threads and 28 MB of
            // private memory, because every monitor needed a device of its
            // own: a Direct3D immediate context belongs to one thread at a
            // time, and sharing one across threads crashed inside the row copy.
            // Sequential painting lets every screen share one device and makes
            // that whole class of bug impossible. Measured: 16.6 ms per capture
            // against 13, where the engine this replaced took 33 to 39.
            lock (_engine)
            {
                foreach (var placement in frame.Placements)
                {
                    _chain.Paint(placement.Monitor, buffer + (nint)placement.Offset, frame.Stride);
                }
            }
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

    /// <summary>
    /// Called on a timer. Lets go of the devices, the textures and the desktop
    /// buffer once nobody has taken a screenshot for a while.
    ///
    /// Measured before this existed: the application sat at 285 MB doing
    /// nothing, of which about 95 was the capture engine holding on for a
    /// screenshot that might come in an hour. The first capture after a
    /// release costs a fifth of a second; every one after it is back to 17 ms.
    /// </summary>
    /// <returns>True when something was actually let go of.</returns>
    public bool ReleaseIfIdle(DateTime now)
    {
        if (_disposed || !_watch.Due(now))
        {
            return false;
        }

        lock (_engine)
        {
            _chain.Release();
            _buffers.Drop();
        }

        Log.Default.Info("простой: движок захвата отпущен до следующего снимка");

        return true;
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
