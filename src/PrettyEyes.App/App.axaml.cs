using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrettyEyes.App.Services;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.App;

public partial class App : Application
{
    private OverlaySession? _session;
    private SettingsWindow? _settings;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = AppServices.Build(desktop);
            Services.Hotkeys.Pressed += (_, _) => StartCapture();

            desktop.ShutdownRequested += (_, _) => Services.Hotkeys.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public AppServices? Services { get; private set; }

    /// <summary>
    /// The only place a capture failure is handled: the app lives in the tray
    /// and has to survive until the next hotkey press.
    /// </summary>
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

    private void Capture_OnClick(object? sender, EventArgs e) => StartCapture();

    private void Settings_OnClick(object? sender, EventArgs e) => OpenSettings();

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
            Services.SettingsStore, Services.Hotkeys, Services.Autostart, Services.Settings);

        _settings.Closed += (_, _) =>
        {
            if (_settings is not null)
            {
                Services.Settings = _settings.Current;
            }

            _settings = null;
        };

        _settings.Show();
    }

    private void Exit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}