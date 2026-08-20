using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;
using PrettyEyes.Platform.Windows;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.App.Services;

/// <summary>
/// Composition root. Everything the app needs is created here once, in a known
/// order, so no other class has to go looking for its dependencies.
/// </summary>
public sealed class AppServices : IDisposable
{
    private AppServices(
        HostWindow host,
        IMonitorEnumerator monitors,
        IScreenCapture capture,
        IImageSink clipboard,
        IImageSink file,
        FolderSink folder,
        INotifier notifier,
        IHotkeys hotkeys,
        ISettingsStore settingsStore,
        IAutostart autostart,
        IPointerLocation pointer,
        Win32TrayIcon tray,
        OverlayWindowPool overlayWindows,
        EmojiAtlas emoji,
        UpdateService updates,
        AppSettings settings)
    {
        Host = host;
        Monitors = monitors;
        Capture = capture;
        Clipboard = clipboard;
        File = file;
        Folder = folder;
        Notifier = notifier;
        Hotkeys = hotkeys;
        SettingsStore = settingsStore;
        Autostart = autostart;
        Pointer = pointer;
        Tray = tray;
        OverlayWindows = overlayWindows;
        Emoji = emoji;
        Updates = updates;
        Settings = settings;
    }

    public HostWindow Host { get; }

    public IMonitorEnumerator Monitors { get; }

    public IScreenCapture Capture { get; }

    public IImageSink Clipboard { get; }

    public IImageSink File { get; }

    /// <summary>Silent writes into the chosen folder, when that is switched on.</summary>
    public FolderSink Folder { get; }

    public INotifier Notifier { get; }

    public IHotkeys Hotkeys { get; }

    public ISettingsStore SettingsStore { get; }

    public IAutostart Autostart { get; }

    public IPointerLocation Pointer { get; }

    public Win32TrayIcon Tray { get; }

    /// <summary>Overlay windows, built once and reused between captures.</summary>
    public OverlayWindowPool OverlayWindows { get; }

    /// <summary>The bundled emoji, decoded once at start-up.</summary>
    public EmojiAtlas Emoji { get; }

    /// <summary>Release checks and the installer handover.</summary>
    public UpdateService Updates { get; }

    /// <summary>
    /// Whether an overlay is up. Set by the application, which is the only
    /// thing that knows: an update must not close the app mid-selection.
    /// </summary>
    public Func<bool>? IsCapturing { get; set; }

    /// <summary>False when a combination was taken at startup.</summary>
    public bool RegionHotkeyRegistered { get; set; }

    public bool FullScreenHotkeyRegistered { get; set; }

    /// <summary>
    /// Windows.Graphics.Capture where the system has it, GDI otherwise. Only
    /// the newer path sees hardware-accelerated windows; the older one is kept
    /// because it needs nothing from the OS beyond BitBlt.
    /// </summary>
    private static IScreenCapture CreateCapture(IMonitorEnumerator monitors) =>
        WgcScreenCapture.IsSupported
            ? new WgcScreenCapture(monitors, ReportSlowStep)
            : new GdiScreenCapture(monitors);

    /// <summary>
    /// Only the steps that took long enough to notice. A line per step per
    /// capture would bury the log; a capture that suddenly costs three times
    /// its usual is exactly what the log is for.
    /// </summary>
    private static void ReportSlowStep(string step, double milliseconds)
    {
        if (milliseconds >= 15)
        {
            Log.Default.Info($"скриншот, шаг {step}: {milliseconds:F1} мс");
        }
    }

    /// <summary>Last known settings; the settings window updates them.</summary>
    public AppSettings Settings { get; set; }

    public static AppServices Build(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        // No main window: closing the last overlay must not quit the app.
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var host = new HostWindow();
        host.ShowHidden();

        var monitors = new Win32MonitorEnumerator();


        // Our own tray icon: Avalonia's only comes with a native menu, and a
        // native menu cannot be made to look like the rest of the app.
        var tray = new Win32TrayIcon("prettyeyes");
        var notifier = new ToastNotifier(AppIdentity.AppUserModelId, tray);

        // Warmed here, before the first hotkey: building an overlay window
        // takes 69 ms the first time, and that time belongs to start-up.
        var overlayWindows = new OverlayWindowPool();
        overlayWindows.Warm(monitors.Enumerate().Monitors.Count);

        // Decoded in the background: forty PNGs on a frozen screen would be a
        // pause the user can see.
        var emoji = new EmojiAtlas();
        _ = emoji.WarmAsync();

        var settingsStore = new JsonSettingsStore(JsonSettingsStore.DefaultPath);
        var settings = settingsStore.Load();

        // Asks the finished services for the current options every time rather
        // than capturing them: the settings window replaces the whole record on
        // every change, and a captured copy would go stale on the first edit.
        AppServices? built = null;
        var folder = new FolderSink(
            () => built?.Settings.Save ?? SaveOptions.Default,
            () => DateTimeOffset.Now);

        // Late-bound the same way the folder sink is: the settings record is
        // replaced whole on every change, so a captured copy goes stale.
        var updates = new UpdateService(
            new GitHubUpdateSource(),
            () => built?.Settings.CheckUpdates ?? true,
            () => built?.IsCapturing?.Invoke() ?? false);

        // Built here and not lazily: the message-only window belongs to the
        // thread that creates it, and this runs on the UI thread.
        var hotkeys = new WindowsHotkeys();
        var regionRegistered = hotkeys.TryRegister(HotkeyAction.Region, settings.Hotkey);
        var fullScreenRegistered = hotkeys.TryRegister(HotkeyAction.FullScreen, settings.FullScreenHotkey);

        foreach (var (registered, hotkey) in new[]
        {
            (regionRegistered, settings.Hotkey),
            (fullScreenRegistered, settings.FullScreenHotkey),
        })
        {
            if (!registered)
            {
                notifier.Notify(
                    "prettyeyes",
                    $"{HotkeyBox.Busy(hotkey)} Смени её в настройках.");
            }
        }

        built = new AppServices(
            host,
            monitors,
            CreateCapture(monitors),
            new WindowsClipboard(),
            new FileSink(host.StorageProvider, () => DateTimeOffset.Now),
            folder,
            notifier,
            hotkeys,
            settingsStore,
            new RegistryAutostart(),
            new Win32PointerLocation(),
            tray,
            overlayWindows,
            emoji,
            updates,
            settings)
        {
            RegionHotkeyRegistered = regionRegistered,
            FullScreenHotkeyRegistered = fullScreenRegistered,
        };

        return built;
    }

    /// <summary>
    /// Takes one capture and throws it away, in the background.
    ///
    /// The first capture of a run costs 128 ms against 44 ms for the ones after
    /// it: the code has to be compiled, the capture item built, the D3D device
    /// woken. That belongs to start-up, not to the first time the hotkey is
    /// pressed. Nothing is stored, shown or written anywhere.
    /// </summary>
    public void WarmCapture() => Task.Run(() =>
    {
        try
        {
            using var scope = Log.Default.Scope("capture.warm");
            Capture.CaptureAll().Image.Dispose();
        }
        catch (InvalidOperationException error)
        {
            // A failed warm-up changes nothing: the real capture reports for
            // itself, and this one nobody asked for.
            Log.Default.Error("прогрев захвата не удался", error);
        }
    });

    /// <summary>
    /// Called on shutdown. The D3D11 device and the tray entry belong to us and
    /// should not wait for process teardown to be released.
    /// </summary>
    public void Dispose()
    {
        Hotkeys.Dispose();
        Tray.Dispose();
        Emoji.Dispose();
        Updates.Dispose();
        (Capture as IDisposable)?.Dispose();
    }
}
