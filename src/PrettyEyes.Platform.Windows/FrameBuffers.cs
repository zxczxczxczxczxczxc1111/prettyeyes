using System.Runtime.InteropServices;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Holds on to the desktop-sized buffer between captures.
///
/// Freshly allocated memory is not backed by physical pages until it is
/// written, and writing 28 MB of it costs a page fault per 4 KB. Measured: the
/// row copy took 108 ms on a new buffer against 15 ms on one that had been
/// used before. Allocating and freeing per capture meant paying that on every
/// capture that followed an idle spell, warm-up included - the warm-up handed
/// its buffer straight back to the system.
///
/// One buffer is kept. A second capture whose image is still alive gets a fresh
/// allocation and hands it back here when it dies; the pool keeps whichever
/// arrives first and frees the rest.
/// </summary>
public sealed unsafe class FrameBuffers : IDisposable
{
    private readonly object _gate = new();

    private IntPtr _spare;
    private nuint _spareSize;
    private bool _disposed;

    /// <summary>A buffer of exactly this size, zeroed only when it is new.</summary>
    public IntPtr Rent(nuint size, bool zeroed)
    {
        lock (_gate)
        {
            if (_spare != IntPtr.Zero && _spareSize == size)
            {
                var reused = _spare;
                _spare = IntPtr.Zero;
                _spareSize = 0;

                if (zeroed)
                {
                    NativeMemory.Clear((void*)reused, size);
                }

                return reused;
            }
        }

        return zeroed
            ? (IntPtr)NativeMemory.AllocZeroed(size)
            : (IntPtr)NativeMemory.Alloc(size);
    }

    /// <summary>
    /// Takes a buffer back. Called from Skia when the image built on it dies,
    /// which can be on any thread and long after the capture.
    /// </summary>
    public void Return(IntPtr buffer, nuint size)
    {
        if (buffer == IntPtr.Zero)
        {
            return;
        }

        lock (_gate)
        {
            if (!_disposed && _spare == IntPtr.Zero)
            {
                _spare = buffer;
                _spareSize = size;

                return;
            }
        }

        NativeMemory.Free((void*)buffer);
    }

    /// <summary>
    /// Gives the spare buffer back to the system.
    ///
    /// Costs the next capture its page faults again (measured: 108 ms against
    /// 15), which is the right trade after minutes of doing nothing and the
    /// wrong one between two screenshots in a row.
    /// </summary>
    public void Drop()
    {
        lock (_gate)
        {
            if (_spare == IntPtr.Zero)
            {
                return;
            }

            NativeMemory.Free((void*)_spare);
            _spare = IntPtr.Zero;
            _spareSize = 0;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;

            if (_spare != IntPtr.Zero)
            {
                NativeMemory.Free((void*)_spare);
                _spare = IntPtr.Zero;
                _spareSize = 0;
            }
        }
    }
}
