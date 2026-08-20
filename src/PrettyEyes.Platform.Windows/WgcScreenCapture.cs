using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;
using SkiaSharp;
using WinRT;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using global::Windows.Graphics.Capture;
using global::Windows.Security.Authorization.AppCapabilityAccess;
using global::Windows.Graphics.DirectX;
using global::Windows.Graphics.DirectX.Direct3D11;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Captures the desktop through Windows.Graphics.Capture. Unlike the GDI path
/// this sees hardware-accelerated windows - video and games come out as they
/// look on screen instead of black. DRM-protected content stays black, which
/// is the point of DRM and not fixable here.
///
/// One capture item per monitor: the API has no virtual-desktop item, so the
/// monitors are grabbed separately and written into one frame.
///
/// Items and frame pools are built once and kept: measured on two 2K monitors,
/// building and tearing them down again cost about 40 ms of the 96 ms a capture
/// took. Capture sessions are not kept forever, because a live session is what
/// makes Windows draw the yellow border and light up the recording indicator.
/// </summary>
public sealed unsafe class WgcScreenCapture : IScreenCapture, IDisposable
{
    /// <summary>A frame normally arrives within a frame or two of the display.</summary>
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly IMonitorEnumerator _monitors;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDirect3DDevice _winrtDevice;
    private readonly Dictionary<IntPtr, MonitorCapture> _captures = [];
    private readonly MtaWorker _worker = new();
    private readonly FrameBuffers _buffers = new();

    /// <summary>
    /// Where the per-step timings go. Null in the application: the numbers are
    /// only wanted by the benchmark, and measuring the parts is the only way to
    /// know which one is worth optimising.
    /// </summary>
    private readonly Action<string, double>? _timing;

    private bool _disposed;

    public WgcScreenCapture(IMonitorEnumerator monitors, Action<string, double>? timing = null)
    {
        _monitors = monitors;
        _timing = timing;

        // BgraSupport is required: the capture frames arrive as BGRA textures.
        var result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
            out _device!,
            out _context!);

        if (result.Failure)
        {
            throw new InvalidOperationException($"Could not create a Direct3D 11 device ({result.Description}).");
        }

        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        _winrtDevice = CaptureInterop.CreateDirect3DDevice(dxgiDevice.NativePointer);
    }

    /// <summary>True when this Windows build has the capture API at all.</summary>
    public static bool IsSupported => GraphicsCaptureSession.IsSupported();

    /// <summary>
    /// Whether Windows agreed to leave the yellow capture border off.
    ///
    /// Setting IsBorderRequired to false is not enough on its own: Windows 11
    /// keeps drawing the border until the user has consented through
    /// RequestAccessAsync, and the documented way to ask is a packaged app with
    /// the graphicsCaptureWithoutBorder capability. Asked once anyway, because
    /// the answer costs a call and the alternative is guessing. What comes back
    /// goes in the log; nothing else depends on it.
    /// </summary>
    private static bool? _borderless;

    private static void AskForBorderless()
    {
        if (_borderless is not null)
        {
            return;
        }

        try
        {
            if (!global::Windows.Foundation.Metadata.ApiInformation.IsTypePresent(
                    "Windows.Graphics.Capture.GraphicsCaptureAccess"))
            {
                _borderless = false;
                Log.Default.Info("рамка захвата: эта сборка Windows про неё не знает");

                return;
            }

            var access = GraphicsCaptureAccess
                .RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            _borderless = access == AppCapabilityAccessStatus.Allowed;
            Log.Default.Info($"рамка захвата: {access}");
        }
        catch (Exception error)
        {
            // A refusal is an answer; an exception is one too. Neither is worth
            // failing a screenshot over.
            _borderless = false;
            Log.Default.Error("не удалось спросить про рамку захвата", error);
        }
    }

    /// <summary>
    /// Runs on the worker thread, never on the caller's.
    ///
    /// The application's UI thread is a single-threaded apartment, and every
    /// WinRT call made from there is marshalled. Measured on the same machine:
    /// 33 ms per capture from a multi-threaded apartment against 105-132 ms
    /// from the UI thread, for the same code and the same monitors.
    /// </summary>
    public CaptureResult CaptureAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _worker.Run(CaptureOnWorker);
    }

    private CaptureResult CaptureOnWorker()
    {
        AskForBorderless();

        var layout = _monitors.Enumerate();
        var bounds = layout.VirtualBounds;

        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Virtual desktop reported a non-positive size.");
        }

        var handles = Win32MonitorEnumerator.Handles();

        // One buffer for the whole desktop, handed over to Skia at the end and
        // taken back here when the image dies.
        var stride = bounds.Width * 4;
        var size = (nuint)((long)stride * bounds.Height);

        // Zeroed only when the monitors do not tile the virtual desktop
        // exactly: on a plain side-by-side setup every byte is overwritten
        // anyway, and clearing 28 MB is not free.
        var covered = layout.Monitors.Sum(monitor => (long)monitor.Bounds.Width * monitor.Bounds.Height);
        var frame = Step("alloc", () => _buffers.Rent(size, zeroed: covered < (long)bounds.Width * bounds.Height));

        try
        {
            // In parallel because every monitor spends most of its capture
            // waiting for a frame, and those waits are the same 16 ms whether
            // they happen one after another or at once. The copy inside is
            // serialised: a D3D11 device context is single-threaded.
            Parallel.ForEach(layout.Monitors, monitor =>
            {
                if (!handles.TryGetValue(monitor.DeviceId, out var handle))
                {
                    throw new InvalidOperationException($"No display handle for {monitor.DeviceId}.");
                }

                var offset = ((monitor.Bounds.Y - bounds.Y) * (long)stride)
                    + ((monitor.Bounds.X - bounds.X) * 4L);

                CaptureMonitor(handle, monitor.Bounds, frame + (nint)offset, stride);
            });
        }
        catch (AggregateException error)
        {
            _buffers.Return(frame, size);

            // One monitor failing is the whole capture failing; the first
            // reason is the useful one.
            throw error.InnerException ?? error;
        }
        catch
        {
            _buffers.Return(frame, size);
            throw;
        }

        var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // FromPixels over our own buffer, not FromPixelCopy: Skia hands the
        // memory back through the release callback when the image dies. That is
        // one 28 MB copy less on every capture.
        using var pixmap = new SKPixmap(info, frame, stride);

        var image = Step("image", () => SKImage.FromPixels(pixmap, (address, _) => _buffers.Return(address, size), null))
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

        _worker.Dispose();
        _buffers.Dispose();

        lock (_captures)
        {
            foreach (var capture in _captures.Values)
            {
                capture.Dispose();
            }

            _captures.Clear();
        }

        _winrtDevice.Dispose();
        _context.Dispose();
        _device.Dispose();
    }

    /// <summary>Times one step and reports it, when anyone is listening.</summary>
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

    private void CaptureMonitor(IntPtr handle, CaptureRect monitorBounds, IntPtr destination, int stride)
    {
        MonitorCapture capture;

        lock (_captures)
        {
            if (!_captures.TryGetValue(handle, out var existing) || !existing.Matches(monitorBounds))
            {
                existing?.Dispose();
                existing = Step("item", () => new MonitorCapture(_winrtDevice, handle, monitorBounds));
                _captures[handle] = existing;
            }

            capture = existing;
        }

        var captured = capture.Capture(
            FrameTimeout,
            frame => CopyPixels(frame, monitorBounds, destination, stride),
            _timing);

        if (!captured)
        {
            throw new InvalidOperationException("Windows.Graphics.Capture delivered no frame in time.");
        }
    }

    /// <summary>
    /// Frame texture to desktop buffer, one copy per row and nothing more.
    /// </summary>
    private void CopyPixels(Direct3D11CaptureFrame frame, CaptureRect monitorBounds, IntPtr destination, int stride)
    {
        var surface = MarshalInterface<IDirect3DSurface>.FromManaged(frame.Surface);
        IntPtr texturePointer;

        try
        {
            texturePointer = CaptureInterop.GetDxgiInterface(surface, typeof(ID3D11Texture2D).GUID);
        }
        finally
        {
            Marshal.Release(surface);
        }

        using var source = new ID3D11Texture2D(texturePointer);
        var description = source.Description;

        // A staging texture is the only kind the CPU is allowed to read.
        // ID3D11Device itself is free-threaded; only the context is not.
        using var staging = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = description.Width,
            Height = description.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = description.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        });

        // The device context is single-threaded, and monitors are captured in
        // parallel.
        MappedSubresource mapped;

        lock (_context)
        {
            _context.CopyResource(staging, source);
            mapped = Step("map", () => _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None));
        }

        try
        {
            // ContentSize, not the texture size: Windows rounds the texture up
            // and whatever sits past the content is leftover memory.
            var width = Math.Min(frame.ContentSize.Width, monitorBounds.Width);
            var height = Math.Min(frame.ContentSize.Height, monitorBounds.Height);
            var pitch = (nint)mapped.RowPitch;
            var origin = mapped.DataPointer;

            Step("rows", () =>
            {
                // Rows straight across, nothing else.
                //
                // The alpha byte is left exactly as the capture delivered it,
                // which is usually zero: Skia ignores it entirely for an image
                // declared SKAlphaType.Opaque. That was checked rather than
                // assumed - the encoded PNG comes out with alpha 255 either
                // way. Fixing it by hand cost a pass over 3.7 million pixels
                // per monitor for nothing.
                var bytes = (nuint)(width * 4);

                for (var row = 0; row < height; row++)
                {
                    NativeMemory.Copy(
                        (void*)(origin + (row * pitch)),
                        (void*)(destination + (row * (nint)stride)),
                        bytes);
                }

                return 0;
            });
        }
        finally
        {
            lock (_context)
            {
                _context.Unmap(staging, 0);
            }
        }
    }

    /// <summary>
    /// A single thread in the multi-threaded apartment, kept for the life of
    /// the capture. Everything WinRT touches happens here.
    /// </summary>
    private sealed class MtaWorker : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private readonly Thread _thread;

        public MtaWorker()
        {
            _thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "prettyeyes capture",
            };

            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        public T Run<T>(Func<T> work)
        {
            using var done = new ManualResetEventSlim(false);
            T result = default!;
            Exception? failure = null;

            _queue.Add(() =>
            {
                try
                {
                    result = work();
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    done.Set();
                }
            });

            done.Wait();

            if (failure is not null)
            {
                // Rethrown on the caller's thread with the original stack kept.
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return result;
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(2));
            _queue.Dispose();
        }

        private void Pump()
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        }
    }

    /// <summary>
    /// One monitor's capture item, kept between captures. Creating it costs
    /// 11.5 ms for two monitors and it never changes while the monitor is
    /// there.
    ///
    /// The frame pool and the session are not kept, and neither is negotiable:
    /// a pool accepts exactly one session in its lifetime (a second one fails
    /// with "Catastrophic failure"), and Windows.Graphics.Capture only produces
    /// frames when the picture changes - so a session left running delivers
    /// nothing at all on a still desktop. Starting a session is what forces the
    /// first frame out. It also means an idle tray application never holds a
    /// live screen recording.
    /// </summary>
    private sealed class MonitorCapture : IDisposable
    {
        private readonly object _gate = new();
        private readonly IDirect3DDevice _device;
        private readonly GraphicsCaptureItem _item;
        private readonly CaptureRect _bounds;

        public MonitorCapture(IDirect3DDevice device, IntPtr handle, CaptureRect bounds)
        {
            _device = device;
            _bounds = bounds;
            _item = CaptureInterop.CreateItemForMonitor(handle);
        }

        public bool Matches(CaptureRect bounds) => _bounds == bounds;

        /// <summary>
        /// Runs one capture and hands the frame to the caller. The frame belongs
        /// to the pool, so it cannot outlive this call.
        /// </summary>
        public bool Capture(TimeSpan timeout, Action<Direct3D11CaptureFrame> use, Action<string, double>? timing)
        {
            lock (_gate)
            {
                var watch = Stopwatch.StartNew();

                using var arrived = new ManualResetEventSlim(false);

                // One buffer is enough: the session is stopped before the frame
                // is read, so nothing else is competing for it.
                using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, _item.Size);

                // Set, never read after disposal: the event outlives the pool
                // because it is declared first and disposed last.
                pool.FrameArrived += (_, _) => arrived.Set();

                using (var session = pool.CreateCaptureSession(_item))
                {
                    session.IsCursorCaptureEnabled = false;

                    // The yellow capture border is a Windows 11 addition; older
                    // builds do not know the property and do not draw it either.
                    if (global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                            "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
                    {
                        session.IsBorderRequired = false;
                    }

                    session.StartCapture();

                    if (!arrived.Wait(timeout))
                    {
                        return false;
                    }
                }

                using var frame = pool.TryGetNextFrame();
                watch.Stop();
                timing?.Invoke("pool+session+frame", watch.Elapsed.TotalMilliseconds);

                if (frame is null)
                {
                    return false;
                }

                use(frame);

                return true;
            }
        }

        public void Dispose()
        {
        }
    }
}
