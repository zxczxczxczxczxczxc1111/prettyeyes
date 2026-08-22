using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.Core.Capture;

/// <summary>
/// A painter that is not built until somebody asks it to paint.
///
/// The spare engines cost real money to exist: Windows.Graphics.Capture builds
/// a Direct3D device and a thread in its constructor, and on a machine where
/// duplication works it will never be asked for a single pixel. Building all
/// three engines at start-up was paying for two of them forever.
///
/// The name is given up front because the chain writes it into the log before
/// anything is built.
/// </summary>
public sealed class LazyPainter : IMonitorPainter
{
    private readonly Func<IMonitorPainter> _build;
    private readonly object _gate = new();

    private IMonitorPainter? _real;
    private bool _disposed;

    public LazyPainter(string name, Func<IMonitorPainter> build)
    {
        Name = name;
        _build = build;
    }

    public string Name { get; }

    /// <summary>True once the engine behind this one actually exists.</summary>
    public bool Built
    {
        get
        {
            lock (_gate)
            {
                return _real is not null;
            }
        }
    }

    public void Paint(MonitorInfo monitor, IntPtr destination, int stride)
    {
        IMonitorPainter real;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            real = _real ??= _build();
        }

        real.Paint(monitor, destination, stride);
    }

    /// <summary>
    /// Throws away the engine behind this one. Unlike Dispose this is not the
    /// end: the next capture builds it again.
    /// </summary>
    public void Release()
    {
        IMonitorPainter? built;

        lock (_gate)
        {
            built = _real;
            _real = null;
        }

        built?.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Nothing to dispose when nothing was ever built, which is the
            // normal case for a spare.
            _real?.Dispose();
            _real = null;
        }
    }
}
