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
    internal const uint WM_APP = 0x8000;
    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_RBUTTONUP = 0x0205;

    internal const int NIF_MESSAGE = 0x00000001;
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

    internal const int IDI_APPLICATION = 32512;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr icon);

    /// <summary>
    /// Explorer broadcasts TaskbarCreated after a restart; everyone who owns a
    /// tray icon has to add it again or it stays gone.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

    internal const int HWND_MESSAGE = -3;
    internal const int WM_HOTKEY = 0x0312;
    internal const uint VK_SNAPSHOT = 0x2C;

    internal delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WndClassEx
    {
        internal int cbSize;
        internal int style;
        internal WndProc lpfnWndProc;
        internal int cbClsExtra;
        internal int cbWndExtra;
        internal IntPtr hInstance;
        internal IntPtr hIcon;
        internal IntPtr hCursor;
        internal IntPtr hbrBackground;
        internal string? lpszMenuName;
        internal string lpszClassName;
        internal IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE, with DWMWCP_ROUND as the value.</summary>
    internal const int DwmWindowCornerPreference = 33;
    internal const int DwmCornerRound = 2;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

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

    internal const int GWL_EXSTYLE = -20;

    /// <summary>A tool window never shows up in Alt+Tab.</summary>
    internal const long WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>Forces a window into the switcher; the opposite of the above.</summary>
    internal const long WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    /// <summary>A window with this style is composited with an alpha of its own.</summary>
    internal const long WS_EX_LAYERED = 0x00080000;

    /// <summary>Which of the two arguments below actually mean anything.</summary>
    internal const uint LWA_ALPHA = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
}
