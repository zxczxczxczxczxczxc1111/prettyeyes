using System.Runtime.InteropServices;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;
using SkiaSharp;
using WinRT;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using global::Windows.Graphics.Capture;
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
/// monitors are grabbed separately and pasted into one frame.
/// </summary>
public sealed class WgcScreenCapture : IScreenCapture, IDisposable
{
    /// <summary>A frame normally arrives within a frame or two of the display.</summary>
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly IMonitorEnumerator _monitors;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDirect3DDevice _winrtDevice;
    private bool _disposed;

    public WgcScreenCapture(IMonitorEnumerator monitors)
    {
        _monitors = monitors;

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

    public CaptureResult CaptureAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var layout = _monitors.Enumerate();
        var bounds = layout.VirtualBounds;

        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Virtual desktop reported a non-positive size.");
        }

        var handles = Win32MonitorEnumerator.Handles();

        var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {bounds.Width}x{bounds.Height} surface.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        foreach (var monitor in layout.Monitors)
        {
            if (!handles.TryGetValue(monitor.DeviceId, out var handle))
            {
                throw new InvalidOperationException($"No display handle for {monitor.DeviceId}.");
            }

            using var image = CaptureMonitor(handle, monitor.Bounds);
            canvas.DrawImage(image, monitor.Bounds.X - bounds.X, monitor.Bounds.Y - bounds.Y);
        }

        return new CaptureResult(surface.Snapshot(), layout);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _winrtDevice.Dispose();
        _context.Dispose();
        _device.Dispose();
    }

    private SKImage CaptureMonitor(IntPtr handle, CaptureRect monitorBounds)
    {
        var item = CaptureInterop.CreateItemForMonitor(handle);

        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);

        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        // The yellow capture border is a Windows 11 addition; older builds do
        // not know the property and do not draw the border either.
        if (global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
        {
            session.IsBorderRequired = false;
        }

        using var arrived = new ManualResetEventSlim(false);
        Direct3D11CaptureFrame? frame = null;

        pool.FrameArrived += (sender, _) =>
        {
            frame ??= sender.TryGetNextFrame();

            if (frame is not null)
            {
                arrived.Set();
            }
        };

        session.StartCapture();

        if (!arrived.Wait(FrameTimeout) || frame is null)
        {
            throw new InvalidOperationException("Windows.Graphics.Capture delivered no frame in time.");
        }

        try
        {
            return ToSkImage(frame, monitorBounds);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private SKImage ToSkImage(Direct3D11CaptureFrame frame, CaptureRect monitorBounds)
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

        _context.CopyResource(staging, source);

        var mapped = _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

        try
        {
            // Only the monitor's own size is kept: the frame can be larger than
            // the display when Windows rounds the texture up.
            var width = Math.Min((int)description.Width, monitorBounds.Width);
            var height = Math.Min((int)description.Height, monitorBounds.Height);

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            var buffer = new byte[width * height * 4];

            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(mapped.DataPointer + (row * (int)mapped.RowPitch), buffer, row * width * 4, width * 4);
            }

            // The capture leaves alpha unset on opaque desktops; force it.
            for (var i = 3; i < buffer.Length; i += 4)
            {
                buffer[i] = 255;
            }

            return SKImage.FromPixelCopy(info, buffer)
                ?? throw new InvalidOperationException("Skia rejected the captured pixel buffer.");
        }
        finally
        {
            _context.Unmap(staging, 0);
        }
    }
}
