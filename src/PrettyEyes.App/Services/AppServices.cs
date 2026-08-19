using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.App.Services;

/// <summary>
/// Composition root. Everything the app needs is created here once, in a known
/// order, so no other class has to go looking for its dependencies.
/// </summary>
public sealed class AppServices
{
    private AppServices(
        HostWindow host,
        IMonitorEnumerator monitors,
        IScreenCapture capture,
        IImageSink clipboard,
        IImageSink file,
        INotifier notifier,
        IHotkeys hotkeys)
    {
        Host = host;
        Monitors = monitors;
        Capture = capture;
        Clipboard = clipboard;
        File = file;
        Notifier = notifier;
        Hotkeys = hotkeys;
    }

    public HostWindow Host { get; }

    public IMonitorEnumerator Monitors { get; }

    public IScreenCapture Capture { get; }

    public IImageSink Clipboard { get; }

    public IImageSink File { get; }

    public INotifier Notifier { get; }

    public IHotkeys Hotkeys { get; }

    public static AppServices Build(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        // No main window: closing the last overlay must not quit the app.
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var host = new HostWindow();
        host.ShowHidden();

        var monitors = new Win32MonitorEnumerator();

        var clipboard = host.Clipboard
            ?? throw new InvalidOperationException("The host window exposes no clipboard.");

        var notifier = new TrayNotifier(host.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

        // Built here and not lazily: the message-only window belongs to the
        // thread that creates it, and this runs on the UI thread.
        var hotkeys = new WindowsHotkeys();
        var hotkey = HotkeyDefinition.Default;

        if (!hotkeys.TryRegister(hotkey))
        {
            notifier.Notify(
                "prettyeyes",
                $"Комбинация {Describe(hotkey)} занята другой программой. Смени её в настройках.");
        }

        return new AppServices(
            host,
            monitors,
            new GdiScreenCapture(monitors),
            new ClipboardSink(clipboard),
            new FileSink(host.StorageProvider, () => DateTimeOffset.Now),
            notifier,
            hotkeys);
    }

    /// <summary>Human-readable combination, e.g. "Ctrl + Shift + 4".</summary>
    public static string Describe(HotkeyDefinition hotkey)
    {
        var parts = new List<string>();

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(hotkey.VirtualKey == NativeKeys.PrintScreen
            ? "PrtScn"
            : ((char)hotkey.VirtualKey).ToString());

        return string.Join(" + ", parts);
    }
}
