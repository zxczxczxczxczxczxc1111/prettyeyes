using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Named mutex the installer checks before replacing the executable.
        // Its name carries the flavour: the check build must not silently exit
        // just because the real one is already running.
        using var instance = new Mutex(initiallyOwned: true, AppFlavor.Current.MutexName, out var isFirst);

        if (!isFirst)
        {
            // Someone already runs it: a second tray icon and a second hotkey
            // registration help nobody.
            return;
        }

        // Nothing here is allowed to die quietly: a tray application shows no
        // window when it crashes, so the log file is the only witness.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error)
            {
                Log.Default.Error("необработанное исключение", error);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Default.Error("незамеченная ошибка задачи", e.Exception);
            e.SetObserved();
        };

        // Must happen before any window exists, so the shell groups them under
        // the same identity the installer's shortcut carries.
        AppIdentity.Declare(AppFlavor.Current.AppUserModelId);

        // The build, not just the version: an hour went into finding out
// which of two 1.3.0 builds wrote a log of five thousand lines.
        Log.Default.Info($"запуск {BuildLabel.Current}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Log.Default.Error("приложение упало на старте или в цикле сообщений", error);
            throw;
        }

        Log.Default.Info("выход");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

#if DEBUG
        // The app keeps its own log; Avalonia's trace listener is for us while
        // developing, not for the people running the release.
        builder = builder.LogToTrace();
#endif

        return builder;
    }
}
