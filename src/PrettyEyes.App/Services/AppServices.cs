using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Services;

/// <summary>
/// Composition root. Everything the app needs is created here once, in a known
/// order, so no other class has to go looking for its dependencies.
/// </summary>
public sealed class AppServices
{
    private AppServices(HostWindow host, IMonitorEnumerator monitors, IScreenCapture capture)
    {
        Host = host;
        Monitors = monitors;
        Capture = capture;
    }

    public HostWindow Host { get; }

    public IMonitorEnumerator Monitors { get; }

    public IScreenCapture Capture { get; }

    public static AppServices Build(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        // No main window: closing the last overlay must not quit the app.
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var host = new HostWindow();
        host.ShowHidden();

        var monitors = new Win32MonitorEnumerator();

        return new AppServices(host, monitors, new GdiScreenCapture(monitors));
    }
}
