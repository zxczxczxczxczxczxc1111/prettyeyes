using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using Avalonia.Platform.Storage;
using PrettyEyes.Core.Rendering;
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
        ShowMagnifier.IsChecked = settings.ShowMagnifier;
        MagnifierGrid.IsChecked = settings.MagnifierGrid;
        MagnifierGrid.IsEnabled = settings.ShowMagnifier;

        var save = settings.Save ?? SaveOptions.Default;
        Autosave.IsChecked = save.Enabled;
        SaveFolder.Text = save.Folder;
        SaveTemplate.Text = save.Template;
        ShowAutosaveState(save.Enabled);
        ShowExample();
        _loading = false;

        RegionHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.Region, hotkey);
        FullScreenHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.FullScreen, hotkey);
        Autostart.IsCheckedChanged += OnAutostartChanged;
        ShowMagnifier.IsCheckedChanged += OnMagnifierChanged;
        MagnifierGrid.IsCheckedChanged += OnMagnifierGridChanged;
        Autosave.IsCheckedChanged += OnAutosaveChanged;
        SaveTemplate.TextChanged += OnTemplateChanged;
        PickFolder.Click += OnPickFolder;

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

    private void OnMagnifierChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = ShowMagnifier.IsChecked == true;

        MagnifierGrid.IsEnabled = enabled;
        Store(_settings with { ShowMagnifier = enabled });
        MagnifierChanged?.Invoke(this, enabled);
    }

    /// <summary>
    /// Switched off, the group stays where it is, greyed out. Hiding it would
    /// mean looking for a setting that is not on the screen.
    /// </summary>
    private void ShowAutosaveState(bool enabled)
    {
        AutosaveOptions.IsEnabled = enabled;
        AutosaveOptions.Opacity = enabled ? 1 : 0.4;
    }

    private void ShowExample() =>
        SaveExample.Text = "например: " + FileNameTemplate.Format(SaveTemplate.Text ?? string.Empty, DateTimeOffset.Now);

    private void OnAutosaveChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = Autosave.IsChecked == true;
        ShowAutosaveState(enabled);
        StoreSave(save => save with { Enabled = enabled });
    }

    private void OnTemplateChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        ShowExample();

        if (_loading)
        {
            return;
        }

        StoreSave(save => save with { Template = SaveTemplate.Text ?? string.Empty });
    }

    /// <summary>
    /// The folder is checked the moment it is picked, not when a screenshot is
    /// waiting on it: a disconnected network drive answers slowly, and that is
    /// not a delay to discover mid-capture.
    /// </summary>
    private async void OnPickFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Куда сохранять снимки",
                AllowMultiple = false,
            });

            var folder = picked.FirstOrDefault()?.TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            if (!FolderSink.CanWrite(folder))
            {
                ShowFailure("В эту папку нельзя писать. Выбери другую.");
                return;
            }

            SaveFolder.Text = folder;
            HideMessage();
            StoreSave(save => save with { Folder = folder });
        }
        catch (Exception error)
        {
            Log.Default.Error("не удалось выбрать папку", error);
            ShowFailure("Не удалось открыть выбор папки.");
        }
    }

    private void StoreSave(Func<SaveOptions, SaveOptions> change) =>
        Store(_settings with { Save = change(_settings.Save ?? SaveOptions.Default) });

    private void OnMagnifierGridChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        Store(_settings with { MagnifierGrid = MagnifierGrid.IsChecked == true });
    }

    /// <summary>The overlay has to hear about this without waiting for a restart.</summary>
    public event EventHandler<bool>? MagnifierChanged;

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
