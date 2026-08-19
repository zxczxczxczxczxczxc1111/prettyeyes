using Avalonia.Controls;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// Two settings, both applied the moment they change: a hotkey that only takes
/// effect after a restart would look broken, and so would a checkbox that
/// claims autostart is on when the registry write failed.
/// </summary>
public partial class SettingsWindow : Window
{
    private ISettingsStore? _store;
    private IHotkeys? _hotkeys;
    private IAutostart? _autostart;

    private AppSettings _settings = AppSettings.Default;
    private bool _loading;

    /// <summary>
    /// Parameterless for the XAML loader; the dependencies arrive through
    /// Configure right after construction.
    /// </summary>
    public SettingsWindow() => InitializeComponent();

    public void Configure(ISettingsStore store, IHotkeys hotkeys, IAutostart autostart, AppSettings settings)
    {
        _store = store;
        _hotkeys = hotkeys;
        _autostart = autostart;
        _settings = settings;

        _loading = true;
        Hotkey.Value = settings.Hotkey;
        Autostart.IsChecked = autostart.IsEnabled;
        _loading = false;

        Hotkey.HotkeyChanged += OnHotkeyChanged;
        Autostart.IsCheckedChanged += OnAutostartChanged;
    }

    /// <summary>The settings as they stand after every applied change.</summary>
    public AppSettings Current => _settings;

    private void OnHotkeyChanged(object? sender, HotkeyDefinition hotkey)
    {
        if (_hotkeys is null)
        {
            return;
        }

        if (_hotkeys.TryRegister(hotkey))
        {
            Hide(Message);
            Apply(_settings with { Hotkey = hotkey });
            return;
        }

        // Put the working combination back so the user is never left without one.
        _hotkeys.TryRegister(_settings.Hotkey);
        Hotkey.Value = _settings.Hotkey;
        Show(Message, "Эта комбинация занята другой программой. Оставил прежнюю.");
    }

    private void OnAutostartChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading || _autostart is null)
        {
            return;
        }

        var wanted = Autostart.IsChecked == true;

        if (_autostart.Set(wanted))
        {
            Hide(Message);
            Apply(_settings with { Autostart = wanted });
            return;
        }

        _loading = true;
        Autostart.IsChecked = !wanted;
        _loading = false;
        Show(Message, "Не удалось изменить автозапуск.");
    }

    private void Apply(AppSettings settings)
    {
        _settings = settings;
        _store?.Save(settings);
    }

    private static void Show(TextBlock message, string text)
    {
        message.Text = text;
        message.IsVisible = true;
    }

    private static void Hide(TextBlock message) => message.IsVisible = false;
}
