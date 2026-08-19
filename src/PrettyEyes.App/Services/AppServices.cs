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
        INotifier notifier)
    {
        Host = host;
        Monitors = monitors;
        Capture = capture;
        Clipboard = clipboard;
        File = file;
        Notifier = notifier;
    }

    public HostWindow Host { get; }

    public IMonitorEnumerator Monitors { get; }

    public IScreenCapture Capture { get; }

    public IImageSink Clipboard { get; }

    public IImageSink File { get; }

    public INotifier Notifier { get; }

    public static AppServices Build(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        // No main window: closing the last overlay must not quit the app.
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var host = new HostWindow();
        host.ShowHidden();

        var monitors = new Win32MonitorEnumerator();

        var clipboard = host.Clipboard
            ?? throw new InvalidOperationException("The host window exposes no clipboard.");

        return new AppServices(
            host,
            monitors,
            new GdiScreenCapture(monitors),
            new ClipboardSink(clipboard),
            new FileSink(host.StorageProvider, () => DateTimeOffset.Now),
            new TrayNotifier(host.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero));
    }
}
