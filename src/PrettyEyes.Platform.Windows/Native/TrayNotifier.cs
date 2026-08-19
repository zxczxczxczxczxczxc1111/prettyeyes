using System.Runtime.InteropServices;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.Platform.Windows.Native;

/// <summary>
/// Balloon notifications through Shell_NotifyIcon. Avalonia draws the tray icon
/// but exposes no way to raise a balloon, so this registers its own tray entry
/// owned by the host window and uses it for messages only.
/// </summary>
public sealed class TrayNotifier : INotifier, IDisposable
{
    private const int IconId = 1;

    private readonly IntPtr _owner;
    private bool _registered;
    private bool _disposed;

    public TrayNotifier(IntPtr ownerWindow) => _owner = ownerWindow;

    public void Notify(string title, string message)
    {
        if (_disposed || _owner == IntPtr.Zero)
        {
            return;
        }

        if (!_registered && !Register())
        {
            // A missing notification is not worth failing a capture over.
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
        if (_disposed || !_registered)
        {
            _disposed = true;
            return;
        }

        _disposed = true;

        var data = NewData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
        _registered = false;
    }

    private bool Register()
    {
        var data = NewData();
        data.uFlags = NativeMethods.NIF_TIP;
        data.szTip = "prettyeyes";

        _registered = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        return _registered;
    }

    private NativeMethods.NotifyIconData NewData() => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        hWnd = _owner,
        uID = IconId,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };
}
