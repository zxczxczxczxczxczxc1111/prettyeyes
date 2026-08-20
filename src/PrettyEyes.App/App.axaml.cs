using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PrettyEyes.App.Services;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;

namespace PrettyEyes.App;

public partial class App : Application
{
    private OverlaySession? _session;
    private SettingsWindow? _settings;
    private TrayMenuWindow? _menu;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = AppServices.Build(desktop);
            Services.Tray.Clicked += (_, _) => Dispatcher.UIThread.Post(StartCapture);
            Services.Tray.MenuRequested += (_, _) => Dispatcher.UIThread.Post(ShowTrayMenu);

            Services.Hotkeys.Pressed += (_, action) =>
            {
                if (action == HotkeyAction.Region)
                {
                    StartCapture();
                }
                else
                {
                    _ = CaptureMonitorAsync();
                }
            };

            // A capture taken before the monitors moved is worthless: the frame
            // and the coordinates no longer describe the same desktop.
            Services.WarmCapture();

            Services.IsCapturing = () => _session is not null;

            // Shown once, and only in the tray balloon: somebody who never
            // opens the settings would otherwise never learn there is a newer
            // version at all.
            Services.Updates.Announced += (_, version) => Services.Notifier.Notify(
                "prettyeyes",
                $"Доступна версия {version}. Обновить можно из меню в трее.");

            Services.Updates.Start();

            // Started with --settings the window opens right away. Useful for a
            // shortcut, and the only way to reach the settings without hunting
            // for the tray icon.
            if (desktop.Args?.Contains("--settings") == true)
            {
                Dispatcher.UIThread.Post(OpenSettings, DispatcherPriority.Background);
            }
            Services.Hotkeys.DisplayChanged += (_, _) => Dispatcher.UIThread.Post(OnDisplayChanged);

            // WM_DISPLAYCHANGE covers a resolution change, but not a monitor
            // rearranged or a scaling change: a message-only window is not even
            // eligible for the broadcast that carries those.
            if (Services.Host.Screens is { } screens)
            {
                screens.Changed += (_, _) => Dispatcher.UIThread.Post(OnDisplayChanged);
            }

            desktop.ShutdownRequested += (_, _) => Services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public AppServices? Services { get; private set; }

    /// <summary>
    /// The only place a capture failure is handled: the app lives in the tray
    /// and has to survive until the next hotkey press.
    /// </summary>
    /// <summary>
    /// The desktop is not what it was. An overlay sized for a monitor that is
    /// gone, or placed at coordinates that no longer exist, is worse than no
    /// overlay at all.
    /// </summary>
    private void OnDisplayChanged()
    {
        if (Services is null)
        {
            return;
        }

        Log.Default.Info("конфигурация мониторов изменилась");
        _session?.Close();
        Services.OverlayWindows.Rebuild(Services.Monitors.Enumerate().Monitors.Count);
    }

    public void StartCapture()
    {
        if (Services is null)
        {
            return;
        }

        if (_session is not null)
        {
            // The overlay is already up: the hotkey means "let me pick again".
            _session.Restart();
            return;
        }

        CaptureResult capture;

        try
        {
            using var scope = Log.Default.Scope("capture");
            capture = Services.Capture.CaptureAll();
        }
        catch (InvalidOperationException ex)
        {
            Services.Notifier.Notify("prettyeyes", $"Не удалось снять экран: {ex.Message}");
            return;
        }

        _session = new OverlaySession(Services);
        _session.Finished += (_, _) => _session = null;
        _session.Start(capture);
    }

    /// <summary>
    /// Whole monitor under the cursor, straight to the clipboard. No overlay:
    /// the point of this hotkey is that nothing gets in the way.
    /// </summary>
    private async Task CaptureMonitorAsync()
    {
        if (Services is null)
        {
            return;
        }

        CaptureResult capture;

        try
        {
            using var scope = Log.Default.Scope("capture");
            capture = Services.Capture.CaptureAll();
        }
        catch (InvalidOperationException ex)
        {
            Services.Notifier.Notify("prettyeyes", $"Не удалось снять экран: {ex.Message}");
            return;
        }

        using var document = new Document(capture.Image, capture.Bounds);
        var (x, y) = Services.Pointer.Current;
        var monitor = capture.Layout.MonitorAt(x, y) ?? capture.Layout.Monitors[0];
        document.Selection = monitor.Bounds;

        var style = Services.Settings.Export ?? ExportStyle.None;
        var transparent = style is { Enabled: true, Background: ExportBackground.Transparent };

        // The clipboard gets a solid background even when the export is
        // transparent: the DIB on the clipboard carries no alpha.
        using var image = DocumentRenderer.Render(
            document,
            transparent ? style with { Background = ExportBackground.Black } : style);

        var result = await Services.Clipboard.SendAsync(image, CancellationToken.None);

        // Autosave applies here too: the point of the setting is that a
        // screenshot ends up in the folder, whichever way it was taken.
        if (Services.Settings.Save?.Ready == true)
        {
            using var forFile = DocumentRenderer.Render(document, style);
            await Services.Folder.SendAsync(forFile, CancellationToken.None);
        }

        Services.Notifier.Notify(
            "prettyeyes",
            result == SinkResult.Sent
                ? "Скриншот монитора скопирован в буфер."
                : "Не удалось скопировать скриншот в буфер.");
    }

    /// <summary>
    /// The tray menu, rebuilt on every right click: it is a short-lived popup,
    /// and keeping one around only invites stale state.
    /// </summary>
    /// <summary>
    /// Opens the folder autosave writes into. Explorer, not a dialog: the point
    /// is to see the files, not to pick one.
    /// </summary>
    private void OpenSnapshotFolder()
    {
        var folder = Services?.Settings.Save?.Folder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception error) when (error is Win32Exception or FileNotFoundException)
        {
            Log.Default.Error("не удалось открыть папку скриншотов", error);
        }
    }

    private void ShowTrayMenu()
    {
        if (Services is null || _menu is not null)
        {
            return;
        }

        var (x, y) = Services.Pointer.Current;
        var layout = Services.Monitors.Enumerate();
        var monitor = layout.MonitorAt(x, y) ?? layout.Monitors[0];

        _menu = new TrayMenuWindow();
        _menu.ShowFolderEntry(Services.Settings.Save?.Ready == true);
        _menu.ShowUpdateEntry(Services.Updates.Found?.Version.ToString());
        _menu.Picked += (_, choice) =>
        {
            switch (choice)
            {
                case TrayMenuChoice.Capture:
                    StartCapture();
                    break;
                case TrayMenuChoice.OpenFolder:
                    OpenSnapshotFolder();
                    break;
                case TrayMenuChoice.Update:
                    _ = InstallUpdateAsync();
                    break;
                case TrayMenuChoice.Settings:
                    OpenSettings();
                    break;
                case TrayMenuChoice.Exit:
                    Exit();
                    break;
            }
        };
        _menu.Closed += (_, _) => _menu = null;

        _menu.ShowAt(x, y, monitor.Bounds, monitor.Scale);
    }

    /// <summary>
    /// Downloads the release and hands over to the installer.
    ///
    /// The app then quits, and it has to: the single-instance mutex is held for
    /// the life of the process, and Inno Setup checks for exactly that mutex
    /// before it replaces the executable.
    /// </summary>
    private async Task InstallUpdateAsync()
    {
        if (Services is null)
        {
            return;
        }

        if (await Services.Updates.InstallAsync())
        {
            Exit();
        }
        else
        {
            Services.Notifier.Notify("prettyeyes", "Не удалось обновиться. Попробуй позже.");
        }
    }

    private void Exit()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void OpenSettings()
    {
        if (Services is null)
        {
            return;
        }

        // One window, reused: a second copy would fight the first over the
        // hotkey registration.
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow();
        _settings.Configure(
            Services.SettingsStore,
            Services.Hotkeys,
            Services.Autostart,
            Services.Settings,
            Services.RegionHotkeyRegistered,
            Services.FullScreenHotkeyRegistered,
            Services.Updates,
            InstallUpdateAsync);

        // Applied the moment it changes, not when the window shuts: the
        // settings window stays open while the next screenshot is taken.
        _settings.Changed += (_, settings) => Services.Settings = settings;

        _settings.Closed += (_, _) =>
        {
            if (_settings is not null)
            {
                Services.Settings = _settings.Current;
            }

            _settings = null;
        };

        _settings.Show();

        // The app has no main window, so a freshly shown one can land behind
        // whatever the user was looking at.
        _settings.Activate();
    }
}