using System.Diagnostics;
using System.Runtime.InteropServices;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Paints a monitor with DXGI Desktop Duplication.
///
/// First in the chain for one reason: Windows draws its yellow capture border
/// around Windows.Graphics.Capture sessions, and nothing an unpackaged
/// application is allowed to do turns that off completely. Measured on 22.08
/// with every documented suppression in place: the border flashed on 24
/// captures out of 45. Through duplication, over 90 captures under a watcher
/// sampling the screen edge every four milliseconds: not one yellow pixel.
///
/// It is also faster: about 10 ms per monitor against 24-30 ms.
///
/// The cursor never arrives in the picture at all. Duplication reports the
/// pointer separately and we never ask, which is why there is nothing here
/// matching the IsCursorCaptureEnabled the older engine has to switch off.
/// </summary>
public sealed class DuplicationPainter : IMonitorPainter
{
    /// <summary>
    /// How long a monitor is given to produce a frame. A capture that took
    /// longer than this would be felt; the older engine gets the monitor
    /// instead, and gets asked again on the next screenshot.
    /// </summary>
    private const int BudgetMs = 300;

    private const int StepMs = 60;

    private readonly AdapterDevices _devices = new();
    private readonly object _gate = new();
    private readonly Dictionary<IntPtr, ID3D11Texture2D> _staging = [];

    private bool _disposed;
    private bool _saidFormat;
    private bool _saidProtected;

    public string Name => "дублирование выхода";

    public void Paint(MonitorInfo monitor, IntPtr destination, int stride)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Win32MonitorEnumerator.Handles().TryGetValue(monitor.DeviceId, out var handle))
        {
            throw new InvalidOperationException($"No display handle for {monitor.DeviceId}.");
        }

        var target = _devices.For(handle, monitor.Bounds);
        var duplication = Duplicate(target);

        try
        {
            Grab(target, duplication, handle, monitor, destination, stride);
        }
        finally
        {
            duplication.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            foreach (var staging in _staging.Values)
            {
                staging.Dispose();
            }

            _staging.Clear();
        }

        _devices.Dispose();
    }

    /// <summary>
    /// Built per capture and dropped straight after.
    ///
    /// Measured: DuplicateOutput costs 1.2 ms, while keeping one alive between
    /// screenshots would save 6 ms and turn a tray application into a screen
    /// recording that never stops. It would also bring back the question this
    /// whole design exists to avoid: whether the frame in hand is still what
    /// the screen looks like, given that duplication only delivers a frame when
    /// the picture changed.
    /// </summary>
    private static IDXGIOutputDuplication Duplicate(AdapterDevices.Target target)
    {
        try
        {
            return target.Output.DuplicateOutput(target.Device);
        }
        catch (SharpGenException error) when (error.ResultCode == Vortice.DXGI.ResultCode.DeviceRemoved)
        {
            throw new InvalidOperationException(
                $"устройство снято: {target.Device.DeviceRemovedReason}", error);
        }
        catch (SharpGenException error) when (
            error.ResultCode == Vortice.DXGI.ResultCode.Unsupported
            || error.ResultCode == Vortice.DXGI.ResultCode.NotCurrentlyAvailable
            || error.ResultCode == Vortice.DXGI.ResultCode.SessionDisconnected
            || error.ResultCode == Result.AccessDenied)
        {
            // Not ours: a hybrid setup that will not duplicate, too many
            // duplications already running on this output, or a session that
            // has been taken over by the secure desktop.
            throw new NotSupportedException($"Windows will not duplicate this output ({error.ResultCode}).", error);
        }
    }

    private void Grab(
        AdapterDevices.Target target,
        IDXGIOutputDuplication duplication,
        IntPtr handle,
        MonitorInfo monitor,
        IntPtr destination,
        int stride)
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.ElapsedMilliseconds < BudgetMs)
        {
            var result = duplication.AcquireNextFrame(StepMs, out var info, out var resource);

            if (result == Vortice.DXGI.ResultCode.WaitTimeout)
            {
                // Nothing changed since the duplication was built a moment ago.
                // With a duplication this young that means the desktop is still,
                // not that we missed anything, so wait out the budget.
                continue;
            }

            if (result == Vortice.DXGI.ResultCode.AccessLost)
            {
                // A mode change, a game taking the screen, the secure desktop
                // coming and going. Not a verdict about this monitor, so the
                // capture fails and the chain asks again next time.
                throw new InvalidOperationException($"Duplication of {monitor.DeviceId} was lost mid-capture.");
            }

            result.CheckError();

            try
            {
                if (info.LastPresentTime == 0)
                {
                    // A frame carrying a mouse move and no desktop update at
                    // all. Measured: 50 of these across 90 captures, so taking
                    // the first frame for the picture would hand back a stale
                    // screenshot more often than not.
                    continue;
                }

                using var texture = resource.QueryInterface<ID3D11Texture2D>();
                Copy(target, texture, info, handle, monitor, destination, stride);

                return;
            }
            finally
            {
                resource.Dispose();
                Release(duplication, monitor);
            }
        }

        throw new InvalidOperationException(
            $"Desktop Duplication gave no frame for {monitor.DeviceId} within {BudgetMs} ms.");
    }

    /// <summary>
    /// Releasing is mandatory: the next AcquireNextFrame answers
    /// DXGI_ERROR_INVALID_CALL without it. It is also allowed to fail on its
    /// own, and since this runs in a finally block, letting it throw would
    /// replace the real reason a capture died with a footnote.
    /// </summary>
    private static void Release(IDXGIOutputDuplication duplication, MonitorInfo monitor)
    {
        try
        {
            duplication.ReleaseFrame();
        }
        catch (SharpGenException error)
        {
            Log.Default.Error($"не удалось отпустить кадр монитора {monitor.DeviceId}", error);
        }
    }

    private void Copy(
        AdapterDevices.Target target,
        ID3D11Texture2D texture,
        OutduplFrameInfo info,
        IntPtr handle,
        MonitorInfo monitor,
        IntPtr destination,
        int stride)
    {
        var description = texture.Description;

        if (description.Format is not (Format.B8G8R8A8_UNorm or Format.B8G8R8A8_UNorm_SRgb))
        {
            // Not ours. Windows.Graphics.Capture asks WinRT to convert for it
            // and gets four bytes per pixel whatever the desktop is made of; we
            // get the desktop as it is. An HDR screen hands back half-precision
            // floats, eight bytes per pixel, and the row copy below would
            // produce a picture half a screen wide made of noise.
            throw new NotSupportedException(
                $"Desktop format {description.Format} is not read by this engine.");
        }

        if (!_saidFormat)
        {
            _saidFormat = true;
            Log.Default.Info($"формат стола: {description.Format}");
        }

        if (info.ProtectedContentMaskedOut && !_saidProtected)
        {
            _saidProtected = true;

            // Worth one line: without it, a black rectangle in a screenshot
            // looks like our bug rather than somebody's DRM.
            Log.Default.Info("часть экрана закрыта защитой содержимого и приходит чёрной");
        }

        var staging = StagingFor(target, handle, description);

        // Monitors share one device and are painted one after another, so this
        // lock is never contended today. It stays as a guard rail: the version
        // that shared a device between monitors painted in parallel crashed
        // inside this very copy on the second capture. If anyone ever brings
        // parallel painting back, this is the line that has to hold.
        lock (target.Context)
        {
            target.Context.CopyResource(staging, texture);

            var mapped = target.Context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

            try
            {
                var width = Math.Min((int)description.Width, monitor.Bounds.Width);
                var height = Math.Min((int)description.Height, monitor.Bounds.Height);
                var pitch = (nint)mapped.RowPitch;
                var bytes = (nuint)(width * 4);

                // Rows straight across, nothing else. The alpha byte is left as
                // the desktop delivered it: Skia ignores it entirely for an
                // image declared opaque, and fixing it by hand would cost a
                // pass over 3.7 million pixels per monitor for nothing.
                for (var row = 0; row < height; row++)
                {
                    unsafe
                    {
                        NativeMemory.Copy(
                            (void*)(mapped.DataPointer + (row * pitch)),
                            (void*)(destination + (row * (nint)stride)),
                            bytes);
                    }
                }
            }
            finally
            {
                target.Context.Unmap(staging, 0);
            }
        }
    }

    /// <summary>
    /// A staging texture is the only kind the CPU may read. Kept per monitor
    /// and rebuilt when the screen changes size, because building one costs
    /// more than the copy it serves.
    /// </summary>
    private ID3D11Texture2D StagingFor(AdapterDevices.Target target, IntPtr handle, Texture2DDescription source)
    {
        lock (_gate)
        {
            if (_staging.TryGetValue(handle, out var known))
            {
                var have = known.Description;

                if (have.Width == source.Width && have.Height == source.Height && have.Format == source.Format)
                {
                    return known;
                }

                known.Dispose();
                _staging.Remove(handle);
            }

            var staging = target.Device.CreateTexture2D(new Texture2DDescription
            {
                Width = source.Width,
                Height = source.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = source.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
            });

            _staging[handle] = staging;

            return staging;
        }
    }
}
