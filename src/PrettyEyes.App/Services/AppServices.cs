using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Stats;
using PrettyEyes.Platform.Windows;
using SkiaSharp;
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
        ShotCounter shots,
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
        Shots = shots;
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

    /// <summary>
    /// Screenshots pinned above everything. Unlike the overlay pool these are
    /// not reused: a pin lives as long as somebody keeps it.
    /// </summary>
    public PinnedWindows Pins { get; } = new();

    /// <summary>The bundled emoji, decoded once at start-up.</summary>
    public EmojiAtlas Emoji { get; }

    /// <summary>Release checks and the installer handover.</summary>
    public UpdateService Updates { get; }

    /// <summary>How many screenshots have been taken, and where they went.</summary>
    public ShotCounter Shots { get; }

    /// <summary>
    /// Why an update must wait, or null when nothing is in the way. Set by the
    /// application, which is the only thing that knows what is open.
    /// </summary>
    public Func<string?>? Busy { get; set; }

    /// <summary>False when a combination was taken at startup.</summary>
    private Timer? _idle;

    /// <summary>
    /// True when nothing of ours is on screen: no overlay, no pinned window, no
    /// settings, no menu. Set by the application, which is the only thing that
    /// knows what is open.
    /// </summary>
    public Func<bool>? Quiet { get; set; }

    private DispatcherTimer? _trim;

    /// <summary>
    /// How long after the last thing the user did the working set is handed
    /// back. Long enough that a burst of screenshots is one trim rather than
    /// twenty, short enough that somebody who takes a shot and then opens Task
    /// Manager sees the small number rather than the big one.
    /// </summary>
    private static readonly TimeSpan TrimAfter = TimeSpan.FromSeconds(4);

    /// <summary>False when a combination was taken at startup.</summary>
    public bool RegionHotkeyRegistered { get; set; }

    public bool FullScreenHotkeyRegistered { get; set; }

    /// <summary>
    /// Painters in the order they are asked, one monitor at a time.
    ///
    /// Desktop Duplication first: it is the only engine here that Windows does
    /// not draw a yellow capture border around, and on this machine it is also
    /// twice as fast. Windows.Graphics.Capture second, for the monitors
    /// duplication refuses: rotated screens and desktops in pixel formats we do
    /// not read. GDI last, because it asks nothing of the system at all and
    /// gets hardware-accelerated windows wrong.
    ///
    /// Nobody is asked whether they support this machine. A painter answers for
    /// one monitor when it is handed one, which is the only question with a
    /// useful answer: on a laptop with an external portrait screen the two
    /// monitors get different answers.
    /// </summary>
    private static IScreenCapture CreateCapture(IMonitorEnumerator monitors)
    {
        // The spares are wrapped rather than built: Windows.Graphics.Capture
        // makes a Direct3D device and a thread in its constructor, and on a
        // machine where duplication works it is never asked for a pixel.
        var painters = new List<IMonitorPainter>
        {
            new DuplicationPainter(),

            // Whether this machine has Windows.Graphics.Capture at all is asked
            // inside the factory rather than here, and that is worth 25 MB:
            // touching the type loads Microsoft.Windows.SDK.NET.dll, the WinRT
            // projection, and it is the third largest module in the process.
            // On a machine where duplication works it is never needed at all.
            new LazyPainter(
                "Windows.Graphics.Capture",
                () => WgcScreenCapture.IsSupported
                    ? new WgcScreenCapture(ReportSlowStep)
                    : throw new NotSupportedException("This Windows build has no Windows.Graphics.Capture.")),

            new LazyPainter("GDI", () => new GdiScreenCapture()),
        };

        return new DesktopCapture(monitors, painters, ReportSlowStep);
    }

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

    /// <summary>
    /// The work is over for now; start counting down to handing the working set
    /// back. Called after a capture, after the settings close, after the menu
    /// closes: every moment where the application has just become a tray icon
    /// again.
    ///
    /// Restarted rather than queued, so a person clicking through ten
    /// screenshots pays for this once, at the end, instead of on every shot.
    /// </summary>
    public void NudgeTrim()
    {
        _trim ??= BuildTrimTimer();
        _trim.Stop();
        _trim.Start();
    }

    private DispatcherTimer BuildTrimTimer()
    {
        var timer = new DispatcherTimer { Interval = TrimAfter };

        timer.Tick += (_, _) =>
        {
            // Not while a pin is on screen: its pixels would be faulted back in
            // on the next repaint, and the person dragging it would pay for the
            // tidier number with a stutter. A pinned screenshot costing memory
            // is honest anyway - it is a picture somebody asked us to keep.
            //
            // The timer repeats, so returning here is how it asks again in four
            // seconds. That is also how a pin being closed gets noticed without
            // anybody having to tell us.
            if (Quiet?.Invoke() == false)
            {
                return;
            }

            timer.Stop();

            if (WorkingSet.Trim())
            {
                Log.Default.Info("рабочий набор возвращён системе (trim)");
            }
        };

        return timer;
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
            () => built?.Busy?.Invoke());

        // Built here and not lazily: the message-only window belongs to the
        // thread that creates it, and this runs on the UI thread.
        var hotkeys = new WindowsHotkeys();
        var regionRegistered = hotkeys.TryRegister(HotkeyAction.Region, settings.Hotkey);
        var fullScreenRegistered = hotkeys.TryRegister(HotkeyAction.FullScreen, settings.FullScreenHotkey);

        // The three pinning ones arrive unassigned and stay that way until
        // somebody types something: registering nothing is not a failure, so
        // they are not part of the warning below either.
        foreach (var (action, hotkey) in new[]
        {
            (HotkeyAction.Pin, settings.PinHotkey),
            (HotkeyAction.HidePinned, settings.HidePinnedHotkey),
            (HotkeyAction.ShowPinned, settings.ShowPinnedHotkey),
        })
        {
            if (hotkey is { Assigned: true })
            {
                hotkeys.TryRegister(action, hotkey);
            }
        }

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

        var autostart = new RegistryAutostart();

        // A dead autostart entry is silent: the checkbox says on, Windows starts
        // nothing, and nobody finds out until they wonder why the tray is empty
        // after a reboot. Found exactly like that on this machine.
        if (autostart.Heal())
        {
            Log.Default.Info("автозапуск указывал в никуда, путь поправлен");
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
            autostart,
            new Win32PointerLocation(),
            tray,
            overlayWindows,
            emoji,
            updates,
            new ShotCounter(new JsonStatsStore(JsonStatsStore.DefaultPath)),
            settings)
        {
            RegionHotkeyRegistered = regionRegistered,
            FullScreenHotkeyRegistered = fullScreenRegistered,
        };

        // After the record exists, because the pins ask it for the current
        // settings on every open rather than keeping a copy.
        built.Pins.Use(built);
        built.WatchForIdle();

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
        catch (Exception error)
        {
            // A failed warm-up changes nothing: the real capture reports for
            // itself, and this one nobody asked for. Caught wide on purpose:
            // the capture chain speaks in NotSupportedException and in whatever
            // DXGI throws, and a warm-up that killed the application over a
            // monitor it could not duplicate would be a fine way to make
            // start-up worse than no warm-up at all.
            Log.Default.Error("прогрев захвата не удался", error);
        }
    });

    /// <summary>
    /// Called on shutdown. The D3D11 device and the tray entry belong to us and
    /// should not wait for process teardown to be released.
    /// </summary>
    /// <summary>
    /// Asks the capture engine, every half minute, whether it has been left
    /// alone long enough to let go of its devices. Half a minute rather than
    /// three: the answer is a comparison of two timestamps, and waking up for
    /// it costs nothing next to what it hands back.
    /// </summary>
    private void WatchForIdle()
    {
        if (Capture is not DesktopCapture capture)
        {
            return;
        }

        _idle = new Timer(
            _ =>
            {
                if (!capture.ReleaseIfIdle(DateTime.Now))
                {
                    return;
                }

                // Only together with the release, never on the plain tick: these
                // are the font and image caches the drawing code lives on, and
                // throwing them away while somebody is working means
                // re-rasterising everything they look at next.
                SKGraphics.PurgeAllCaches();

                // And only after the purge, because trimming first would hand
                // back pages the purge is about to make free anyway. This one
                // frees nothing at all, it only changes what Task Manager
                // reports; see WorkingSet for why that is worth a line of code.
                if (WorkingSet.Trim())
                {
                    // The ASCII tail is not decoration: the measurement script runs in a
                    // console that mangles Cyrillic, and it matches on "trim".
                    Log.Default.Info("простой: рабочий набор возвращён системе (trim)");
                }
            },
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        _trim?.Stop();
        _idle?.Dispose();
        Hotkeys.Dispose();
        Tray.Dispose();
        Emoji.Dispose();
        Updates.Dispose();
        (Capture as IDisposable)?.Dispose();
    }
}
