using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrettyEyes.App.Services;

namespace PrettyEyes.App;

public partial class App : Application
{
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
}