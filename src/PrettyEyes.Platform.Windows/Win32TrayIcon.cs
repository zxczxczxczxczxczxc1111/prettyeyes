using System.Runtime.InteropServices;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Our own tray icon on Shell_NotifyIcon instead of Avalonia's TrayIcon.
/// Avalonia can only pair its icon with a NativeMenu, and a native menu is
/// drawn by Windows - no font, no colours, no animation. Owning the icon means
/// the clicks arrive here and the menu can be an ordinary window of ours.
///
/// Must be constructed on the UI thread: the message window belongs to the
/// message queue of the thread that creates it.
/// </summary>
public sealed class Win32TrayIcon : INotifier, IDisposable
{
    private const int IconId = 1;
    private const uint TrayCallbackMessage = NativeMethods.WM_APP + 1;

    // Held in a field: the delegate is passed to native code, and a collected
    // one crashes the process on the first message.
    private readonly NativeMethods.WndProc _windowProc;
    private readonly string _className = $"PrettyEyesTray_{Guid.NewGuid():N}";
    private readonly IntPtr _hwnd;
    private readonly IntPtr _icon;

    private readonly uint _taskbarCreated;
    private readonly string _tooltip;

    private bool _added;
    private bool _disposed;

    public Win32TrayIcon(string tooltip)
    {
        _windowProc = HandleMessage;

        var instance = NativeMethods.GetModuleHandle(null);
        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = Marshal.SizeOf<NativeMethods.WndClassEx>(),
            lpfnWndProc = _windowProc,
            hInstance = instance,
            lpszClassName = _className,
        };

        if (NativeMethods.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException(
                $"Could not register the tray window class (win32 error {Marshal.GetLastWin32Error()}).");
        }

        _hwnd = NativeMethods.CreateWindowEx(
            0, _className, string.Empty, 0, 0, 0, 0, 0,
            new IntPtr(NativeMethods.HWND_MESSAGE), IntPtr.Zero, instance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not create the tray window (win32 error {Marshal.GetLastWin32Error()}).");
        }

        _icon = LoadOwnIcon();
        _tooltip = tooltip;
        _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        Add(tooltip);
    }

    /// <summary>Left click on the tray icon: the shortest way to a capture.</summary>
    public event EventHandler? Clicked;

    /// <summary>Right click: the menu belongs here.</summary>
    public event EventHandler? MenuRequested;

    public void Notify(string title, string message)
    {
        if (_disposed || !_added)
        {
            return;
        }

        var data = NewData();
        data.uFlags = NativeMethods.NIF_INFO;
        data.szInfo = message;
        data.szInfoTitle = title;
        data.dwInfoFlags = NativeMethods.NIIF_INFO;

        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added)
        {
            var data = NewData();
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            _added = false;
        }

        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
        }
    }

    /// <summary>
    /// Adding can fail while the shell is busy - right after an explorer
    /// restart, for instance. TaskbarCreated brings us back, so a failure here
    /// is not worth taking the application down for.
    /// </summary>
    private void Add(string tooltip)
    {
        var data = NewData();
        data.uFlags = NativeMethods.NIF_ICON | NativeMethods.NIF_TIP | NativeMethods.NIF_MESSAGE;
        data.uCallbackMessage = (int)TrayCallbackMessage;
        data.hIcon = _icon;
        data.szTip = tooltip;

        _added = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
    }

    /// <summary>The application icon, taken straight from our own executable.</summary>
    private static IntPtr LoadOwnIcon()
    {
        var path = Environment.ProcessPath;

        if (string.IsNullOrEmpty(path))
        {
            return IntPtr.Zero;
        }

        // Small icon: the tray draws at 16x16, and the large one would be
        // downscaled badly.
        NativeMethods.ExtractIconEx(path, 0, out var large, out var small, 1);

        if (large != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(large);
        }

        return small;
    }

    private NativeMethods.NotifyIconData NewData() => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        hWnd = _hwnd,
        uID = IconId,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == _taskbarCreated && _taskbarCreated != 0 && !_disposed)
        {
            // Explorer restarted and took the icon with it.
            _added = false;
            Add(_tooltip);
            return IntPtr.Zero;
        }

        if (message == TrayCallbackMessage)
        {
            switch ((uint)lParam.ToInt32())
            {
                case NativeMethods.WM_LBUTTONUP:
                    Clicked?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;

                case NativeMethods.WM_RBUTTONUP:
                    MenuRequested?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;
            }
        }

        return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
    }
}
