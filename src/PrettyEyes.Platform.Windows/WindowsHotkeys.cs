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
    private const uint WM_DISPLAYCHANGE = 0x007E;

    private readonly HashSet<HotkeyAction> _registered = [];

    // Held in a field on purpose: the delegate is passed to native code, and a
    // collected one crashes the process on the first message.
    private readonly NativeMethods.WndProc _windowProc;
    private readonly string _className = $"PrettyEyesHotkey_{Guid.NewGuid():N}";
    private readonly IntPtr _hwnd;

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

    public event EventHandler<HotkeyAction>? Pressed;

    public event EventHandler? DisplayChanged;

    public bool TryRegister(HotkeyAction action, HotkeyDefinition hotkey)
    {
        Unregister(action);

        var registered = NativeMethods.RegisterHotKey(
            _hwnd, IdOf(action), (uint)hotkey.Modifiers, hotkey.VirtualKey);

        if (registered)
        {
            _registered.Add(action);
        }

        return registered;
    }

    public void Unregister(HotkeyAction action)
    {
        if (!_registered.Remove(action))
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, IdOf(action));
    }

    /// <summary>Win32 hotkey ids start at 1; the enum starts at 0.</summary>
    private static int IdOf(HotkeyAction action) => (int)action + 1;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var action in _registered.ToArray())
        {
            Unregister(action);
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
        }
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_HOTKEY)
        {
            var action = (HotkeyAction)(wParam.ToInt32() - 1);

            if (Enum.IsDefined(action))
            {
                Pressed?.Invoke(this, action);
                return IntPtr.Zero;
            }
        }

        if (message == WM_DISPLAYCHANGE)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }

        return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
    }
}
