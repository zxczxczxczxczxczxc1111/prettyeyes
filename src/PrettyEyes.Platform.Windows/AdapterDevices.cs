using PrettyEyes.Core.Geometry;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Finds the DXGI output that drives a monitor and the Direct3D device allowed
/// to duplicate it.
///
/// An output can only be duplicated on a device built on the adapter that
/// drives it, which on a laptop with two graphics chips is not the adapter you
/// would have picked.
///
/// One device per adapter, which on a normal machine means one device for
/// every screen. That is only safe because monitors are painted one after
/// another: a Direct3D 11 immediate context belongs to one thread at a time,
/// and an earlier version that painted in parallel on a shared device crashed
/// inside the row copy on the second capture. Measured cost of going back to
/// one device: 28 MB of private memory and seventeen threads saved, three and
/// a half milliseconds per capture spent.
/// </summary>
internal sealed class AdapterDevices : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<long, Device> _devices = [];
    private readonly Dictionary<IntPtr, Target> _targets = [];

    private bool _disposed;

    /// <summary>Everything one monitor needs, already checked and kept.</summary>
    internal sealed record Target(
        ID3D11Device Device,
        ID3D11DeviceContext Context,
        IDXGIOutput1 Output,
        CaptureRect Bounds);

    private sealed record Device(ID3D11Device Handle, ID3D11DeviceContext Context, IDXGIAdapter1 Adapter);

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> when this monitor is not ours
    /// to duplicate: rotated, gone, or sitting somewhere other than where the
    /// rest of the application thinks it is.
    /// </summary>
    public Target For(IntPtr monitor, CaptureRect bounds)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_targets.TryGetValue(monitor, out var known) && known.Bounds == bounds)
            {
                return known;
            }

            if (known is not null)
            {
                // The monitor moved or changed resolution, so the output we
                // remembered describes a screen that no longer exists.
                known.Output.Dispose();
                _targets.Remove(monitor);
            }

            var found = Locate(monitor, bounds);
            _targets[monitor] = found;

            return found;
        }
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

            foreach (var target in _targets.Values)
            {
                target.Output.Dispose();
            }

            _targets.Clear();

            foreach (var device in _devices.Values)
            {
                device.Context.Dispose();
                device.Handle.Dispose();
                device.Adapter.Dispose();
            }

            _devices.Clear();
        }
    }

    private Target Locate(IntPtr monitor, CaptureRect bounds)
    {
        // A fresh factory every time, and that is on purpose: a factory made
        // before a graphics card was plugged in keeps saying so forever. This
        // runs once per monitor per configuration, not once per screenshot.
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        for (var a = 0u; factory.EnumAdapters1(a, out var adapter).Success; a++)
        {
            var mine = false;

            try
            {
                for (var o = 0u; adapter.EnumOutputs(o, out var output).Success; o++)
                {
                    var description = output.Description;

                    if (description.Monitor != monitor)
                    {
                        output.Dispose();

                        continue;
                    }

                    try
                    {
                        Check(description, bounds);

                        var device = DeviceFor(adapter, monitor);
                        mine = true;

                        return new Target(device.Handle, device.Context, output.QueryInterface<IDXGIOutput1>(), bounds);
                    }
                    finally
                    {
                        // The IDXGIOutput1 handed out above is a separate
                        // reference; this one has done its job either way.
                        output.Dispose();
                    }
                }
            }
            finally
            {
                if (!mine)
                {
                    adapter.Dispose();
                }
            }
        }

        throw new NotSupportedException($"No DXGI output drives monitor {monitor:X}.");
    }

    private static void Check(OutputDescription description, CaptureRect bounds)
    {
        if (description.Rotation is not (ModeRotation.Identity or ModeRotation.Unspecified))
        {
            // A rotated output hands back an unrotated texture, and turning
            // four million pixels back is code nobody here can test: no machine
            // in reach has a portrait screen. An honest refusal sends this one
            // monitor to the older engine and leaves the others alone.
            throw new NotSupportedException(
                $"Monitor is rotated ({description.Rotation}), which this engine does not turn back.");
        }

        var where = description.DesktopCoordinates;

        if (where.Left != bounds.X || where.Top != bounds.Y
            || where.Right - where.Left != bounds.Width
            || where.Bottom - where.Top != bounds.Height)
        {
            // DXGI and the rest of the application disagree about where this
            // screen is. Capturing anyway would put the pixels in the wrong
            // half of the picture, which is worse than not capturing.
            throw new NotSupportedException(
                $"DXGI puts the monitor at {where.Left},{where.Top} and we put it at {bounds.X},{bounds.Y}.");
        }
    }

    private Device DeviceFor(IDXGIAdapter1 adapter, IntPtr monitor)
    {
        var luid = adapter.Description1.Luid;

        if (_devices.TryGetValue(luid, out var known))
        {
            adapter.Dispose();

            return known;
        }

        var created = D3D11.D3D11CreateDevice(
            adapter,
            // Unknown, not Hardware: passing an adapter and a driver type at
            // the same time is an error, and D3D11 answers with a flat
            // E_INVALIDARG that says nothing about why.
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
            out ID3D11Device? handle,
            out ID3D11DeviceContext? context);

        if (created.Failure || handle is null || context is null)
        {
            handle?.Dispose();
            context?.Dispose();
            adapter.Dispose();

            throw new NotSupportedException($"Could not build a device on this adapter ({created.Description}).");
        }

        var device = new Device(handle, context, adapter);
        _devices[luid] = device;

        return device;
    }
}
