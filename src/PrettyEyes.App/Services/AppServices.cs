using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;
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
        IHotkeys hotkeys,
        ISettingsStore settingsStore,
        IAutostart autostart,
        AppSettings settings)
    {
        Host = host;
        Monitors = monitors;
        Capture = capture;
        Clipboard = clipboard;
        File = file;
        Notifier = notifier;
        Hotkeys = hotkeys;
        SettingsStore = settingsStore;
        Autostart = autostart;
        Settings = settings;
    }

    public HostWindow Host { get; }

    public IMonitorEnumerator Monitors { get; }

    public IScreenCapture Capture { get; }

    public IImageSink Clipboard { get; }

    public IImageSink File { get; }

    public INotifier Notifier { get; }

    public IHotkeys Hotkeys { get; }

    public ISettingsStore SettingsStore { get; }

    public IAutostart Autostart { get; }

    /// <summary>Last known settings; the settings window updates them.</summary>
    public AppSettings Settings { get; set; }

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

        var settingsStore = new JsonSettingsStore(JsonSettingsStore.DefaultPath);
        var settings = settingsStore.Load();

        // Built here and not lazily: the message-only window belongs to the
        // thread that creates it, and this runs on the UI thread.
        var hotkeys = new WindowsHotkeys();
        var hotkey = settings.Hotkey;

        if (!hotkeys.TryRegister(hotkey))
        {
            notifier.Notify(
                "prettyeyes",
                $"Комбинация {HotkeyBox.Describe(hotkey)} занята другой программой. Смени её в настройках.");
        }

        return new AppServices(
            host,
            monitors,
            new GdiScreenCapture(monitors),
            new ClipboardSink(clipboard),
            new FileSink(host.StorageProvider, () => DateTimeOffset.Now),
            notifier,
            hotkeys,
            settingsStore,
            new RegistryAutostart(),
            settings);
    }
}
