using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Enumerates monitors through EnumDisplayMonitors. Deliberately not using
/// Avalonia's Screens: its constructor is [PrivateApi] and an instance only
/// exists once a TopLevel does, but capture happens before any window opens.
/// </summary>
public sealed class Win32MonitorEnumerator : IMonitorEnumerator
{
    private const int MdtEffectiveDpi = 0;
    private const double BaselineDpi = 96.0;

    public DesktopLayout Enumerate()
    {
        var monitors = new List<MonitorInfo>();

        var enumerated = NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero,
            (IntPtr handle, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
            {
                var info = new NativeMethods.MonitorInfoEx
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                };

                if (NativeMethods.GetMonitorInfo(handle, ref info))
                {
                    monitors.Add(ToMonitorInfo(handle, info));
                }

                return true;
            },
            IntPtr.Zero);

        if (!enumerated || monitors.Count == 0)
        {
            throw new InvalidOperationException("Windows reported no monitors.");
        }

        return new DesktopLayout(monitors);
    }

    /// <summary>
    /// Device name to HMONITOR. Handles never leave this project: Core speaks
    /// device ids, and the capture layer needs the handle to ask for an item.
    /// </summary>
    internal static IReadOnlyDictionary<string, IntPtr> Handles()
    {
        var handles = new Dictionary<string, IntPtr>();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero,
            (IntPtr handle, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
            {
                var info = new NativeMethods.MonitorInfoEx
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                };

                if (NativeMethods.GetMonitorInfo(handle, ref info))
                {
                    handles[info.szDevice] = handle;
                }

                return true;
            },
            IntPtr.Zero);

        return handles;
    }

    private static MonitorInfo ToMonitorInfo(IntPtr handle, NativeMethods.MonitorInfoEx info)
    {
        var r = info.rcMonitor;
        var bounds = new CaptureRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        var w = info.rcWork;
        var work = new CaptureRect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);

        var scale = 1.0;
        if (NativeMethods.GetDpiForMonitor(handle, MdtEffectiveDpi, out var dpiX, out _) == 0)
        {
            scale = dpiX / BaselineDpi;
        }

        return new MonitorInfo(info.szDevice, bounds, scale, work.IsEmpty ? null : work);
    }
}
