using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
using PrettyEyes.Core.Diagnostics;
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
        using var instance = new Mutex(initiallyOwned: true, "PrettyEyesSingleInstance", out var isFirst);

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
        AppIdentity.Declare();

        Log.Default.Info("запуск");

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
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
