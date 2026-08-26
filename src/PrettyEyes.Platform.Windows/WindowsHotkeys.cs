using PrettyEyes.Core.Diagnostics;
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

    /// <summary>
    /// How often the held keys are asked whether they are still down. Three
    /// frames: fast enough that a deliberate second press is never swallowed,
    /// slow enough to be free.
    /// </summary>
    private const uint ReleasePoll = 50;

    /// <summary>Any id will do; the window has one timer and this is it.</summary>
    private static readonly IntPtr PollId = new(1);

    /// <summary>
    /// Actions whose key has fired and has not been let go of since.
    ///
    /// Windows repeats WM_HOTKEY for as long as the key is held, and a repeat
    /// is not a second press: holding the whole-monitor key used to take eight
    /// screenshots in two seconds, and before there was any guard at all it
    /// took the machine down. What a person means by holding a key is one
    /// action, so the repeats are dropped and the key has to come up before the
    /// action arms again.
    ///
    /// The value is the virtual key, kept because WM_HOTKEY carries it and
    /// GetAsyncKeyState needs it.
    /// </summary>
    private readonly Dictionary<HotkeyAction, int> _held = [];

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

        // The poll goes with the window it was set on. Left behind, it would
        // keep asking about keys nobody is listening for any more.
        NativeMethods.KillTimer(_hwnd, PollId);
        _held.Clear();

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
        }
    }


    /// <summary>
    /// Raises the action, unless this is the same key still being held down.
    ///
    /// A held key repeats through Windows key repeat, roughly thirty times a
    /// second, and every repeat arrives here as an ordinary WM_HOTKEY. There is
    /// no flag on the message saying which is which, so the answer has to come
    /// from the key itself: fired once, the action stays disarmed until
    /// GetAsyncKeyState reports the key up.
    /// </summary>
    private void Fire(HotkeyAction action, int virtualKey)
    {
        if (_held.ContainsKey(action))
        {
            return;
        }

        // Recorded before the handler runs, not after: the handler can be slow
        // (a whole-monitor shot is a capture, a render and a clipboard write),
        // and repeats arriving while it works must already find the action
        // disarmed.
        _held[action] = virtualKey;

        if (_held.Count == 1)
        {
            NativeMethods.SetTimer(_hwnd, PollId, ReleasePoll, IntPtr.Zero);
        }

        Pressed?.Invoke(this, action);
    }

    /// <summary>
    /// Arms again every action whose key has come up, and stops asking once
    /// none are left.
    /// </summary>
    private void ReleaseFinished()
    {
        foreach (var (action, virtualKey) in _held.ToArray())
        {
            if ((NativeMethods.GetAsyncKeyState(virtualKey) & NativeMethods.KeyDown) == 0)
            {
                _held.Remove(action);
            }
        }

        if (_held.Count == 0)
        {
            NativeMethods.KillTimer(_hwnd, PollId);
        }
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_HOTKEY)
        {
            var action = (HotkeyAction)(wParam.ToInt32() - 1);

            if (Enum.IsDefined(action))
            {
                // The virtual key rides in the high word of lParam, which is
                // the only place it is available here: the registration lives
                // in the settings and this window never sees it.
                // How long the keystroke took to reach us. Everything the log
                // says about a screenshot starts after this point, so a slow
                // hand-over from Windows - a busy foreground application, a
                // message queue stuck behind something else - reads as an
                // application that sat still for a moment and then woke up.
                var waited = Environment.TickCount - NativeMethods.GetMessageTime();

                if (waited >= 16)
                {
                    Log.Default.Info($"клавиша шла до приложения {waited} мс");
                }

                Fire(action, (lParam.ToInt32() >> 16) & 0xFFFF);
                return IntPtr.Zero;
            }
        }

        if (message == NativeMethods.WM_TIMER && lParam == IntPtr.Zero && wParam == PollId)
        {
            ReleaseFinished();
            return IntPtr.Zero;
        }

        if (message == WM_DISPLAYCHANGE)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }

        return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
    }
}
