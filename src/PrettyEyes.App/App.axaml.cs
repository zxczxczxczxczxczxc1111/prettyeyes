using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrettyEyes.App.Services;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.App;

public partial class App : Application
{
    private OverlaySession? _session;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = AppServices.Build(desktop);
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
        if (Services is null || _session is not null)
        {
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

    private void Exit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}