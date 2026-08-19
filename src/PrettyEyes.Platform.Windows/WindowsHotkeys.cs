using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Global hotkey on a message-only window.
///
/// RegisterHotKey(IntPtr.Zero, ...) would post WM_HOTKEY to the thread queue
/// with a null window handle, and Avalonia's message loop drops those without
/// a trace - the hotkey would simply never fire and never report an error.
/// So this owns a real HWND parented to HWND_MESSAGE.
///
/// Must be constructed on the UI thread: the window belongs to the message
/// queue of the thread that created it.
/// </summary>
public sealed class WindowsHotkeys : IHotkeys
{
    private const int HotkeyId = 1;

    // Held in a field on purpose: the delegate is passed to native code, and a
    // collected one crashes the process on the first message.
    private readonly NativeMethods.WndProc _windowProc;
    private readonly string _className = $"PrettyEyesHotkey_{Guid.NewGuid():N}";
    private readonly IntPtr _hwnd;

    private bool _registered;
    private bool _disposed;

    public WindowsHotkeys()
    {
        _windowProc = HandleMessage;

        var instance = NativeMethods.GetModuleHandle(null);
        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WndClassEx>(),
            lpfnWndProc = _windowProc,
            hInstance = instance,
            lpszClassName = _className,
        };

        if (NativeMethods.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException(
                $"Could not register the hotkey window class (win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        }

        _hwnd = NativeMethods.CreateWindowEx(
            0, _className, string.Empty, 0, 0, 0, 0, 0,
            new IntPtr(NativeMethods.HWND_MESSAGE), IntPtr.Zero, instance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not create the hotkey window (win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        }
    }

    public event EventHandler? Pressed;

    public bool TryRegister(HotkeyDefinition hotkey)
    {
        Unregister();

        _registered = NativeMethods.RegisterHotKey(
            _hwnd, HotkeyId, (uint)hotkey.Modifiers, hotkey.VirtualKey);

        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unregister();

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
        }
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
    }
}
