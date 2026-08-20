using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Views;

/// <summary>
/// Two hotkeys and one switch, all applied the moment they change: a hotkey
/// that only takes effect after a restart would look broken, and so would a
/// checkbox that claims autostart is on when the registry write failed.
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
    public SettingsWindow()
    {
        InitializeComponent();

        // The window has no system frame, so dragging is ours to implement.
        TitleBar.PointerPressed += (_, e) => BeginMoveDrag(e);
        CloseButton.Click += (_, _) => Close();

        // Rounded corners come from the compositor, not from a transparent
        // window: see WindowCorners.
        Opened += (_, _) =>
        {
            WindowCorners.Round(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

            // Fades in rather than snapping into place; the transition lives in
            // the XAML and needs the value set after the first layout pass.
            Dispatcher.UIThread.Post(
                () =>
                {
                    Card.Opacity = 1;
                    Card.RenderTransform = TransformOperations.Parse("translateY(0px)");
                },
                DispatcherPriority.Loaded);
        };
    }

    /// <summary>The settings as they stand after every applied change.</summary>
    public AppSettings Current => _settings;

    public void Configure(
        ISettingsStore store,
        IHotkeys hotkeys,
        IAutostart autostart,
        AppSettings settings,
        bool regionRegistered,
        bool fullScreenRegistered)
    {
        _store = store;
        _hotkeys = hotkeys;
        _autostart = autostart;
        _settings = settings;

        _loading = true;
        RegionHotkey.Value = settings.Hotkey;
        FullScreenHotkey.Value = settings.FullScreenHotkey;
        Autostart.IsChecked = autostart.IsEnabled;
        _loading = false;

        RegionHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.Region, hotkey);
        FullScreenHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.FullScreen, hotkey);
        Autostart.IsCheckedChanged += OnAutostartChanged;

        if (!regionRegistered || !fullScreenRegistered)
        {
            ShowWarning("Комбинация занята другой программой. Выбери другую.");
        }
    }

    private void Apply(HotkeyAction action, HotkeyDefinition hotkey)
    {
        if (_hotkeys is null)
        {
            return;
        }

        if (Taken(action, hotkey))
        {
            Restore(action);
            ShowWarning("Эта комбинация уже занята вторым хоткеем. Оставил прежнюю.");
            return;
        }

        if (_hotkeys.TryRegister(action, hotkey))
        {
            HideMessage();
            Store(action == HotkeyAction.Region
                ? _settings with { Hotkey = hotkey }
                : _settings with { FullScreenHotkey = hotkey });
            return;
        }

        // Put the working combination back so the user is never left without one.
        _hotkeys.TryRegister(action, Existing(action));
        Restore(action);
        ShowWarning("Эта комбинация занята другой программой. Оставил прежнюю.");
    }

    /// <summary>Our own two hotkeys must not fight over one combination.</summary>
    private bool Taken(HotkeyAction action, HotkeyDefinition hotkey) =>
        action == HotkeyAction.Region
            ? hotkey == _settings.FullScreenHotkey
            : hotkey == _settings.Hotkey;

    private HotkeyDefinition Existing(HotkeyAction action) =>
        action == HotkeyAction.Region ? _settings.Hotkey : _settings.FullScreenHotkey;

    private void Restore(HotkeyAction action)
    {
        _loading = true;

        if (action == HotkeyAction.Region)
        {
            RegionHotkey.Value = _settings.Hotkey;
        }
        else
        {
            FullScreenHotkey.Value = _settings.FullScreenHotkey;
        }

        _loading = false;
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
            HideMessage();
            Store(_settings with { Autostart = wanted });
            return;
        }

        _loading = true;
        Autostart.IsChecked = !wanted;
        _loading = false;
        ShowFailure("Не удалось изменить автозапуск.");
    }

    private void Store(AppSettings settings)
    {
        _settings = settings;

        // A setting that looks applied but was never written down comes back
        // wrong after a restart, and nobody connects that to this moment.
        if (_store?.Save(settings) == false)
        {
            Log.Default.Info("не удалось сохранить настройки");
            ShowFailure("Настройки применены, но не сохранились.");
        }
    }

    private void ShowWarning(string text) => Show(text, "Warn");

    private void ShowFailure(string text) => Show(text, "Danger");

    private void Show(string text, string brushKey)
    {
        Message.Text = text;
        Message.Foreground = (IBrush)Application.Current!.FindResource(brushKey)!;
        Message.IsVisible = true;
    }

    private void HideMessage() => Message.IsVisible = false;
}
