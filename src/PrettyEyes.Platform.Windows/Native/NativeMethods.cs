using System.Runtime.InteropServices;

namespace PrettyEyes.Platform.Windows.Native;

internal static class NativeMethods
{
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    internal const int SRCCOPY = 0x00CC0020;
    internal const int MONITORINFOF_PRIMARY = 1;

    internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        internal int cbSize;
        internal Rect rcMonitor;
        internal Rect rcWork;
        internal int dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal int biSize;
        internal int biWidth;
        internal int biHeight;
        internal short biPlanes;
        internal short biBitCount;
        internal int biCompression;
        internal int biSizeImage;
        internal int biXPelsPerMeter;
        internal int biYPelsPerMeter;
        internal int biClrUsed;
        internal int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader bmiHeader;
        internal int bmiColors;
    }

    internal const int NIM_ADD = 0x00000000;
    internal const int NIM_DELETE = 0x00000002;
    internal const int NIM_MODIFY = 0x00000001;
    internal const int NIF_ICON = 0x00000002;
    internal const int NIF_TIP = 0x00000004;
    internal const int NIF_INFO = 0x00000010;
    internal const int NIIF_INFO = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        internal int cbSize;
        internal IntPtr hWnd;
        internal int uID;
        internal int uFlags;
        internal int uCallbackMessage;
        internal IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string szTip;

        internal int dwState;
        internal int dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string szInfo;

        internal int uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string szInfoTitle;

        internal int dwInfoFlags;
        internal Guid guidItem;
        internal IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    /// <summary>
    /// Per-monitor DPI. Available since Windows 8.1; the process manifest makes
    /// it return real values instead of a virtualized 96.
    /// </summary>
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteDC(IntPtr hdc);

    // SetLastError is required: the caller reports Marshal.GetLastWin32Error().
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool BitBlt(
        IntPtr dest, int xDest, int yDest, int width, int height,
        IntPtr src, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    internal static extern int GetDIBits(
        IntPtr hdc, IntPtr bitmap, uint start, uint lines,
        byte[] bits, ref BitmapInfo info, uint usage);
}
