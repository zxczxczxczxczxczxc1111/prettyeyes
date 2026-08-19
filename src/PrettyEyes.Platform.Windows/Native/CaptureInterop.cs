using System.Runtime.InteropServices;
using WinRT;
using global::Windows.Graphics.Capture;

namespace PrettyEyes.Platform.Windows.Native;

/// <summary>
/// The two bridges Windows.Graphics.Capture needs and the WinRT projection does
/// not provide: turning a DXGI device into a WinRT IDirect3DDevice, and asking
/// the capture factory for an item that stands for a whole monitor.
///
/// The COM calls go through the vtable directly. Declaring the interfaces with
/// ComImport looks tidier but fails at runtime here: these are raw IUnknown
/// interfaces on objects the WinRT projection owns, and the built-in marshaller
/// refuses the cast.
/// </summary>
internal static unsafe class CaptureInterop
{
    private const int CreateForMonitorSlot = 4;
    private const int GetInterfaceSlot = 3;

    /// <summary>
    /// IID of IGraphicsCaptureItem, the default interface of the runtime class.
    /// typeof(GraphicsCaptureItem).GUID is not it, and CreateForMonitor answers
    /// a wrong IID with E_NOINTERFACE rather than something readable.
    /// </summary>
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid CaptureItemInteropIid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid DxgiInterfaceAccessIid = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    /// <summary>Wraps a DXGI device in the WinRT device the capture API takes.</summary>
    internal static global::Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice CreateDirect3DDevice(IntPtr dxgiDevice)
    {
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable);

        if (hr != 0)
        {
            throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice failed (hr 0x{hr:X8}).");
        }

        try
        {
            return MarshalInterface<global::Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>A capture item covering one monitor, from its HMONITOR.</summary>
    internal static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
    {
        using var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");

        // The interop methods live on their own interface, not on the
        // activation factory's own vtable.
        var interop = QueryInterface(factory.ThisPtr, CaptureItemInteropIid);

        try
        {
            var iid = GraphicsCaptureItemIid;
            var vtable = *(void***)interop;
            var createForMonitor =
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtable[CreateForMonitorSlot];

            IntPtr item;
            var hr = createForMonitor(interop, monitor, &iid, &item);

            if (hr != 0 || item == IntPtr.Zero)
            {
                throw new InvalidOperationException($"CreateForMonitor failed (hr 0x{hr:X8}).");
            }

            try
            {
                return GraphicsCaptureItem.FromAbi(item);
            }
            finally
            {
                Marshal.Release(item);
            }
        }
        finally
        {
            Marshal.Release(interop);
        }
    }

    /// <summary>The D3D11 texture behind a captured frame's surface.</summary>
    internal static IntPtr GetDxgiInterface(IntPtr surface, Guid iid)
    {
        var access = QueryInterface(surface, DxgiInterfaceAccessIid);

        try
        {
            var vtable = *(void***)access;
            var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vtable[GetInterfaceSlot];

            IntPtr result;
            var hr = getInterface(access, &iid, &result);

            if (hr != 0 || result == IntPtr.Zero)
            {
                throw new InvalidOperationException($"IDirect3DDxgiInterfaceAccess.GetInterface failed (hr 0x{hr:X8}).");
            }

            return result;
        }
        finally
        {
            Marshal.Release(access);
        }
    }

    private static IntPtr QueryInterface(IntPtr unknown, Guid iid)
    {
        var hr = Marshal.QueryInterface(unknown, in iid, out var result);

        if (hr != 0 || result == IntPtr.Zero)
        {
            throw new InvalidOperationException($"QueryInterface for {iid} failed (hr 0x{hr:X8}).");
        }

        return result;
    }
}
