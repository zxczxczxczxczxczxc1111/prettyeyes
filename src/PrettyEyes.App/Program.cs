using Avalonia;
using System;
using System.Threading;

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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
