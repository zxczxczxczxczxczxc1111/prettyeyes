using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;
using PrettyEyes.Core.Rendering;
using PrettyEyes.App.Services;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using PrettyEyes.Core.Updates;
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
    private UpdateService? _updates;
    private Func<Task>? _install;
    private IHotkeys? _hotkeys;
    private IAutostart? _autostart;

    private AppSettings _settings = AppSettings.Default;
    private ToolVisibility _tools = new();
    private ExportStyle _export = ExportStyle.None;
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
        bool fullScreenRegistered,
        UpdateService updates,
        Func<Task> install)
    {
        _store = store;
        _updates = updates;
        _install = install;
        _hotkeys = hotkeys;
        _autostart = autostart;
        _settings = settings;

        _loading = true;
        RegionHotkey.Value = settings.Hotkey;
        FullScreenHotkey.Value = settings.FullScreenHotkey;
        Autostart.IsChecked = autostart.IsEnabled;
        ShowMagnifierRow();

        var save = settings.Save ?? SaveOptions.Default;
        Autosave.IsChecked = save.Enabled;
        SaveFolder.Text = save.Folder;
        SaveTemplate.Text = save.Template;
        ShowAutosaveState(save.Enabled);
        ShowExample();

        _tools = new ToolVisibility(settings.Tools);
        BuildToolRow();
        BuildCursorRow();

        CheckUpdates.IsChecked = settings.CheckUpdates;
        CurrentVersion.Text = $"установлена {UpdateService.Current}";
        ShowUpdateState(updates.State);

        _export = settings.Export ?? ExportStyle.None;
        ExportShadow.IsChecked = _export.Shadow;
        ExportGrain.IsChecked = _export.Grain;
        ExportSheen.IsChecked = _export.Sheen;
        BuildExportRows();
        ShowExportState(_export.Enabled);
        ShowPreview();
        _loading = false;

        RegionHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.Region, hotkey);
        FullScreenHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.FullScreen, hotkey);
        Autostart.IsCheckedChanged += OnAutostartChanged;
        MagnifierOff.Click += (_, _) => ApplyMagnifier(shown: false, grid: _settings.MagnifierGrid);
        MagnifierPlain.Click += (_, _) => ApplyMagnifier(shown: true, grid: false);
        MagnifierWithGrid.Click += (_, _) => ApplyMagnifier(shown: true, grid: true);
        Autosave.IsCheckedChanged += OnAutosaveChanged;
        SaveTemplate.TextChanged += OnTemplateChanged;
        PickFolder.Click += OnPickFolder;
        CheckUpdates.IsCheckedChanged += OnCheckUpdatesChanged;
        CheckNow.Click += (_, _) => _ = updates.CheckAsync(manual: true);
        OpenReleases.Click += (_, _) => OpenPage(GitHubUpdateSource.ReleasesPage);
        InstallUpdate.Click += (_, _) => _ = _install?.Invoke();

        // Unsubscribed on close: the service outlives this window, and a
        // handler pointing at a closed one would write into nothing.
        updates.StateChanged += OnUpdateState;
        Closed += (_, _) => updates.StateChanged -= OnUpdateState;

        ExportShadow.IsCheckedChanged += (_, _) => ApplyExport(_export with { Shadow = ExportShadow.IsChecked == true });
        ExportGrain.IsCheckedChanged += (_, _) => ApplyExport(_export with { Grain = ExportGrain.IsChecked == true });
        ExportSheen.IsCheckedChanged += (_, _) => ApplyExport(_export with { Sheen = ExportSheen.IsChecked == true });
        ExportAdvanced.Click += (_, _) => ShowAdvanced(!ExportOptions.IsVisible);

        if (!regionRegistered || !fullScreenRegistered)
        {
            ShowWarning("Комбинация занята другой программой. Выбери другую.");
        }
    }

    /// <summary>
    /// One button per cursor shape, drawn from the same outline the cursor
    /// itself is drawn from.
    /// </summary>
    private void BuildCursorRow()
    {
        CursorRow.Children.Clear();

        foreach (var style in Crosshair.All)
        {
            var button = new Button
            {
                Tag = style,
                Content = new Avalonia.Controls.Shapes.Path
                {
                    Data = Avalonia.Media.Geometry.Parse(Crosshair.Icon(style)),
                },
            };

            button.Classes.Add("toolpick");
            ToolTip.SetTip(button, CursorName(style));
            button.Click += OnCursorPicked;

            CursorRow.Children.Add(button);
        }

        ShowCursorRow();
    }

    private void ShowCursorRow()
    {
        foreach (var child in CursorRow.Children)
        {
            if (child is not Button button || button.Tag is not CursorStyle style)
            {
                continue;
            }

            button.Classes.Remove("active");

            if (style == _settings.Cursor)
            {
                button.Classes.Add("active");
            }
        }
    }

    private void OnCursorPicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading || sender is not Button button || button.Tag is not CursorStyle style)
        {
            return;
        }

        Store(_settings with { Cursor = style });
        ShowCursorRow();
    }

    private static string CursorName(CursorStyle style) => style switch
    {
        CursorStyle.Cross => "Крест",
        CursorStyle.Gap => "Крест с просветом",
        CursorStyle.Dot => "Точка",
        CursorStyle.Scope => "Прицел",
        _ => "Обычный указатель",
    };

    /// <summary>
    /// One checkbox per tool, named the way the toolbar tooltip names it.
    /// </summary>
    private void BuildToolRow()
    {
        ToolRow.Children.Clear();
        FeatureRow.Children.Clear();

        foreach (var feature in ConfigurableFeature.All)
        {
            var button = NewFeatureButton(feature);

            if (feature.Group == FeatureGroup.DrawingTool)
            {
                button.Click += OnToolToggled;
                ToolRow.Children.Add(button);
            }
            else
            {
                button.Click += OnFeatureToggled;
                FeatureRow.Children.Add(button);
            }
        }

        ShowToolRow();
    }

    /// <summary>
    /// One square in either row. The corner is the whole point of the mark: a
    /// feature with settings has to look different before the right button is
    /// pressed, because an invisible button nobody presses is not an affordance.
    /// Blur is the one square without it.
    /// </summary>
    private static Button NewFeatureButton(ConfigurableFeature feature)
    {
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = Avalonia.Media.Geometry.Parse(FeatureIcon(feature.Id)),
        };

        var content = new Panel();
        content.Children.Add(icon);

        if (feature.HasSettings)
        {
            content.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = Avalonia.Media.Geometry.Parse("M0,6 L6,6 L6,0 Z"),
                Width = 6,
                Height = 6,
                Stretch = Avalonia.Media.Stretch.None,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 3, 3),
                Fill = Avalonia.Media.Brushes.Gray,
                Stroke = null,
            });
        }

        var button = new Button
        {
            Tag = feature,
            Content = content,
        };

        button.Classes.Add("toolpick");
        ToolTip.SetTip(button, FeatureName(feature.Id));

        return button;
    }

    /// <summary>Which of them are on, right now.</summary>
    private void ShowToolRow()
    {
        foreach (var child in ToolRow.Children.Concat(FeatureRow.Children))
        {
            if (child is not Button button || button.Tag is not ConfigurableFeature feature)
            {
                continue;
            }

            button.Classes.Remove("active");

            if (IsFeatureOn(feature))
            {
                button.Classes.Add("active");
            }
        }
    }

    /// <summary>
    /// Whether the square is lit. A drawing tool answers from ToolVisibility;
    /// a feature answers from its own setting, and the cursor answers "yes"
    /// because there is no such thing as having no cursor.
    /// </summary>
    private bool IsFeatureOn(ConfigurableFeature feature) => feature.Id switch
    {
        _ when feature.Tool is { } tool => _tools.IsShown(tool),
        FeatureId.Magnifier => _settings.ShowMagnifier,
        FeatureId.Cursor => true,
        FeatureId.QuickSave => (_settings.Save ?? SaveOptions.Default).Enabled,
        FeatureId.Export => _export.Enabled,
        FeatureId.Pin => _settings.PinButtonShown,
        _ => true,
    };

    /// <summary>
    /// Left click on a feature square. Deliberately not a switch over every
    /// FeatureId: the cursor has no off state, and pretending otherwise would
    /// mean inventing a setting nobody asked for.
    /// </summary>
    private void OnFeatureToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading || sender is not Button button || button.Tag is not ConfigurableFeature feature)
        {
            return;
        }

        switch (feature.Id)
        {
            case FeatureId.Magnifier:
                Store(_settings with { ShowMagnifier = !_settings.ShowMagnifier });
                ShowMagnifierRow();
                break;

            case FeatureId.QuickSave:
                var enabled = !(_settings.Save ?? SaveOptions.Default).Enabled;
                Autosave.IsChecked = enabled;
                ShowAutosaveState(enabled);
                StoreSave(save => save with { Enabled = enabled });
                break;

            case FeatureId.Export:
                ApplyExport(_export with { Enabled = !_export.Enabled });
                break;

            case FeatureId.Pin:
                Store(_settings with { PinButtonShown = !_settings.PinButtonShown });
                break;

            default:
                // Cursor, and anything added later without an off state.
                return;
        }

        ShowToolRow();
    }

    private void OnToolToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading
            || sender is not Button button
            || button.Tag is not ConfigurableFeature { Tool: { } kind })
        {
            return;
        }

        if (!_tools.TrySet(kind, !_tools.IsShown(kind)))
        {
            ShowWarning("Хотя бы один инструмент должен остаться.");

            return;
        }

        ShowToolRow();
        Store(_settings with { Tools = _tools.ToDictionary() });
    }

    /// <summary>
    /// The same outlines the toolbar draws. Duplicated rather than shared: a
    /// resource dictionary for five path strings buys nothing, and the toolbar
    /// markup is where anyone would go looking for them anyway.
    /// </summary>
    private static string ToolIcon(ToolKind kind) => kind switch
    {
        ToolKind.Blur => "M4,4 H6.5 V6.5 H4 Z M9.5,4 H12 V6.5 H9.5 Z M6.5,6.5 H9 V9 H6.5 Z M4,9.5 H6.5 V12 H4 Z M9.5,9.5 H12 V12 H9.5 Z",
        ToolKind.Arrow => "M4,12 L12,4 M12,4 H7.5 M12,4 V8.5",
        ToolKind.Line => "M4,12 L12,4",
        ToolKind.Pencil => "M3.5,12.5 L4.2,9.8 L10,4 L12,6 L6.2,11.8 Z M9,5.2 L10.8,7",
        ToolKind.Marker => "M5.5,9.5 L9.8,5.2 L11.8,7.2 L7.5,11.5 H5.5 Z M3.5,13.5 H12.5",
        ToolKind.Emoji => "M8,2.5 A5.5,5.5 0 1 0 8.01,2.5 M6,6.5 V7 M10,6.5 V7 M5.5,9.5 A3,3 0 0 0 10.5,9.5",
        _ => "M3.5,4 H12.5 V12 H3.5 Z",
    };

    private static string ToolName(ToolKind kind) => kind switch
    {
        ToolKind.Blur => "Размытие",
        ToolKind.Arrow => "Стрелка",
        ToolKind.Line => "Линия",
        ToolKind.Pencil => "Карандаш",
        ToolKind.Marker => "Маркер",
        ToolKind.Emoji => "Эмодзи",
        _ => "Рамка",
    };

    /// <summary>
    /// Icons for both rows. Drawing tools keep the outlines the toolbar uses;
    /// the features borrow shapes from the sections they stand for, so the row
    /// reads as a map of this window rather than as new vocabulary.
    /// </summary>
    private static string FeatureIcon(FeatureId id) => id switch
    {
        FeatureId.Blur => ToolIcon(ToolKind.Blur),
        FeatureId.Arrow => ToolIcon(ToolKind.Arrow),
        FeatureId.Line => ToolIcon(ToolKind.Line),
        FeatureId.Rectangle => ToolIcon(ToolKind.Rectangle),
        FeatureId.Pencil => ToolIcon(ToolKind.Pencil),
        FeatureId.Marker => ToolIcon(ToolKind.Marker),
        FeatureId.Emoji => ToolIcon(ToolKind.Emoji),
        FeatureId.Magnifier => "M7,2.5 A4.5,4.5 0 1 0 7.01,2.5 M10.3,10.3 L13.5,13.5",
        FeatureId.Cursor => "M4,2.5 L4,12 L6.5,9.5 L8.5,13.5 L10,12.7 L8,9 L11.5,8.5 Z",
        FeatureId.QuickSave => "M8,3 V10 M5,7.5 L8,10.5 L11,7.5 M3.5,12.5 H12.5",
        FeatureId.Export => "M3,3.5 H13 V12.5 H3 Z M5.5,6 H10.5 M5.5,8.5 H10.5",
        FeatureId.Pin => "M9.5,2.5 L13.5,6.5 L11,7 L8,11 L5,8 L9,5 Z M5,11 L2.5,13.5",
        _ => "M3.5,4 H12.5 V12 H3.5 Z",
    };

    private static string FeatureName(FeatureId id) => id switch
    {
        FeatureId.Blur => ToolName(ToolKind.Blur),
        FeatureId.Arrow => ToolName(ToolKind.Arrow),
        FeatureId.Line => ToolName(ToolKind.Line),
        FeatureId.Rectangle => ToolName(ToolKind.Rectangle),
        FeatureId.Pencil => ToolName(ToolKind.Pencil),
        FeatureId.Marker => ToolName(ToolKind.Marker),
        FeatureId.Emoji => ToolName(ToolKind.Emoji),
        FeatureId.Magnifier => "Лупа",
        FeatureId.Cursor => "Курсор",
        FeatureId.QuickSave => "Быстрое сохранение",
        FeatureId.Export => "Оформление",
        FeatureId.Pin => "Закрепление",
        _ => "Рамка",
    };

    private void OnCheckUpdatesChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        Store(_settings with { CheckUpdates = CheckUpdates.IsChecked == true });
        _updates?.Reschedule();
        ShowUpdateState(_updates?.State ?? new UpdateState(UpdateStage.Idle));
    }

    /// <summary>
    /// The release notes in a browser. Whether the shell has one, and whether
    /// it feels like opening it, is not something we can do anything about -
    /// but it is not worth taking the settings window down over.
    /// </summary>
    private void OpenPage(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            Log.Default.Error("не удалось открыть страницу релизов", error);
            ShowWarning("Не удалось открыть браузер.");
        }
    }

    private void OnUpdateState(object? sender, UpdateState state) =>
        Dispatcher.UIThread.Post(() => ShowUpdateState(state));

    /// <summary>
    /// The one line that says where the update got to, plus the button that is
    /// only there when there is something to install.
    /// </summary>
    private void ShowUpdateState(UpdateState state)
    {
        var busy = state.Stage is UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Installing;

        CheckNow.IsEnabled = !busy;

        // Also after a failure, as long as a version is known: a download that
        // broke halfway is the case where retrying is most likely to work, and
        // hiding the button would make it a trip through the check first.
        InstallUpdate.IsVisible = state.Version is not null
            && state.Stage is UpdateStage.Available or UpdateStage.Failed;
        InstallUpdate.Content = $"Обновить до {state.Version}";

        UpdateStatus.Text = state.Stage switch
        {
            UpdateStage.Checking => "Проверяю...",
            UpdateStage.UpToDate => "Установлена последняя версия",
            UpdateStage.Available => $"Доступна {state.Version}",
            UpdateStage.Downloading => $"Скачиваю {state.Progress:P0}",
            UpdateStage.Installing => "Запускаю установщик...",
            // A failure with a version behind it is a download that went
            // wrong, not a check: telling somebody the check failed while the
            // installer is what broke sends them looking in the wrong place.
            UpdateStage.Failed => state.Version is null ? "Не удалось проверить" : "Не удалось обновиться",
            _ => string.Empty,
        };
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
        ShowWarning($"{HotkeyBox.Busy(hotkey)} Оставил прежнюю.");
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

    /// <summary>
    /// Off, on, on with the grid. Turning it off keeps whatever grid choice was
    /// made: coming back should land where it was left, not on a default.
    /// </summary>
    private void ApplyMagnifier(bool shown, bool grid)
    {
        if (_loading)
        {
            return;
        }

        Store(_settings with { ShowMagnifier = shown, MagnifierGrid = grid });
        ShowMagnifierRow();
    }

    private void ShowMagnifierRow()
    {
        foreach (var (button, on) in new[]
        {
            (MagnifierOff, !_settings.ShowMagnifier),
            (MagnifierPlain, _settings.ShowMagnifier && !_settings.MagnifierGrid),
            (MagnifierWithGrid, _settings.ShowMagnifier && _settings.MagnifierGrid),
        })
        {
            button.Classes.Remove("active");

            if (on)
            {
                button.Classes.Add("active");
            }
        }
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
                Title = "Куда сохранять скриншоты",
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

    /// <summary>
    /// The three ways a screenshot can leave the application.
    ///
    /// Nothing is stored about which one is picked: a preset is highlighted
    /// when the current style equals it, and `ExportStyle` is a record, so that
    /// comparison is free and cannot drift from the truth. Change a parameter
    /// by hand and no preset lights up, which is exactly what happened.
    /// </summary>
    private static readonly (string Label, ExportStyle Style)[] Presets =
    [
        ("нет", ExportStyle.None),
        ("карточка", ExportStyle.Card),
        ("на белом", ExportStyle.Sheet),
    ];

    /// <summary>
    /// Rendered once, when the window opens. A preset does not change, so
    /// redrawing its picture on every click would be work for an identical
    /// result - and with the aura that work includes a blur.
    /// </summary>
    private readonly Dictionary<string, Bitmap> _presetPreviews = [];

    private void BuildPresetRow()
    {
        PresetRow.Children.Clear();

        foreach (var (label, style) in Presets)
        {
            var chosen = style.Enabled
                ? _export == style
                : !_export.Enabled;

            if (!_presetPreviews.TryGetValue(label, out var preview))
            {
                preview = RenderSample(style);
                _presetPreviews[label] = preview;
            }

            PresetRow.Children.Add(NewPreset(label, preview, chosen, () => ApplyExport(style)));
        }

        // Shown only to whoever went looking for a transparent background in
        // the parameters: from the presets it is not offered at all, because
        // the clipboard cannot deliver it.
        PresetHint.IsVisible = _export is { Enabled: true, Background: ExportBackground.Transparent };
        PresetHint.Text = "Прозрачность попадёт только в файл. Буфер обмена в Windows "
            + "не умеет её переносить, поэтому при вставке фон будет белым.";
    }

    private void ShowAdvanced(bool shown)
    {
        ExportOptions.IsVisible = shown;
        ExportAdvanced.Content = shown ? "Свернуть" : "Настроить";
    }

    /// <summary>
    /// Padding, backdrop and rounding as rows of small buttons. Built in code
    /// because they are three of the same thing, and three copies of the same
    /// markup would drift apart.
    /// </summary>
    private void BuildExportRows()
    {
        BuildPresetRow();
        PaddingRow.Children.Clear();
        BackgroundRow.Children.Clear();
        RadiusRow.Children.Clear();

        foreach (var (label, value) in new (string, int)[] { ("нет", 0), ("24", 24), ("48", 48), ("72", 72) })
        {
            PaddingRow.Children.Add(NewChoice(label, _export.Padding == value, () =>
                ApplyExport(_export with { Padding = value })));
        }

        foreach (var (label, value) in new (string, ExportBackground)[]
        {
            ("чёрный", ExportBackground.Black),
            ("градиент", ExportBackground.Gradient),
            ("дымка", ExportBackground.Aura),
            ("белый", ExportBackground.White),
            ("прозрачный", ExportBackground.Transparent),
        })
        {
            BackgroundRow.Children.Add(NewChoice(label, _export.Background == value, () =>
                ApplyExport(_export with { Background = value })));
        }

        foreach (var value in new[] { 0, 8, 16 })
        {
            RadiusRow.Children.Add(NewChoice(value == 0 ? "нет" : value.ToString(), _export.CornerRadius == value, () =>
                ApplyExport(_export with { CornerRadius = value })));
        }

        // A shadow with no padding falls outside the picture, so the switch
        // says so instead of doing nothing.
        ExportShadow.IsEnabled = _export.ShadowAllowed;
        ExportShadow.IsChecked = _export.Shadow && _export.ShadowAllowed;

        // Grain is backdrop work, and two of the backdrops have nothing for it
        // to work on. The switch says so instead of doing nothing.
        ExportGrain.IsEnabled = _export.GrainApplies;
        ExportGrain.IsChecked = _export.GrainAllowed;
        ExportSheen.IsChecked = _export.Sheen;
    }

    /// <summary>
    /// A preset button: the picture it produces, with its name under it.
    /// Choosing between three results, rather than between three words.
    /// </summary>
    private Button NewPreset(string label, Bitmap preview, bool active, Action pick)
    {
        var stack = new StackPanel { Spacing = 6 };

        stack.Children.Add(new Border
        {
            CornerRadius = new Avalonia.CornerRadius(4),
            ClipToBounds = true,
            Child = new Image { Source = preview, Width = 96, Height = 68, Stretch = Stretch.UniformToFill },
        });

        stack.Children.Add(new TextBlock
        {
            Text = label,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontSize = 11,
        });

        var button = new Button { Content = stack, Padding = new Avalonia.Thickness(6) };

        button.Classes.Add("choice");

        if (active)
        {
            button.Classes.Add("active");
        }

        button.Click += (_, _) => pick();

        return button;
    }

    private Button NewChoice(string label, bool active, Action pick)
    {
        var button = new Button { Content = label };

        button.Classes.Add("choice");

        if (active)
        {
            button.Classes.Add("active");
        }

        button.Click += (_, _) => pick();

        return button;
    }

    private void ApplyExport(ExportStyle style)
    {
        if (_loading)
        {
            return;
        }

        _export = style with { Shadow = style.Shadow && style.ShadowAllowed };

        ShowExportState(_export.Enabled);
        BuildExportRows();
        ShowPreview();
        Store(_settings with { Export = _export });
    }

    /// <summary>
    /// Deliberately does not redraw the preview. It used to, and since the
    /// caller draws it a line later, every change of a parameter rendered the
    /// sample twice - which with the aura means blurring it twice.
    /// </summary>
    private void ShowExportState(bool enabled)
    {
        ExportOptions.IsEnabled = enabled;
        ExportOptions.Opacity = enabled ? 1 : 0.4;
    }

    /// <summary>
    /// A sample screenshot run through the real renderer. Anything else would
    /// be a drawing of what the export might look like.
    /// </summary>
    private void ShowPreview() => ExportPreview.Source = RenderSample(_export);

    /// <summary>
    /// The stand-in screenshot every preview is built from.
    ///
    /// It carries colour on purpose. The old sample was grey lines on grey, and
    /// on a backdrop drawn from the picture's own colours that showed nothing
    /// at all: a preview of the aura has to have something to be an aura of.
    /// The colour lives inside the sample, not in the interface around it.
    /// </summary>
    private static SKImage SampleShot()
    {
        using var surface = SKSurface.Create(new SKImageInfo(320, 200));
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(0x14, 0x14, 0x18));

        using var line = new SKPaint { Color = new SKColor(0x33, 0x33, 0x3A), StrokeWidth = 8 };

        for (var y = 40; y < 200; y += 32)
        {
            canvas.DrawLine(24, y, y < 120 ? 296 : 200, y, line);
        }

        using var accent = new SKPaint { Color = new SKColor(0xB0, 0x10, 0x30), StrokeWidth = 8 };
        canvas.DrawLine(24, 24, 120, 24, accent);

        var dots = new[]
        {
            new SKColor(0x8E, 0x4E, 0xC6),
            new SKColor(0x00, 0x91, 0xFF),
            new SKColor(0x30, 0xA4, 0x6C),
        };

        for (var i = 0; i < dots.Length; i++)
        {
            using var dot = new SKPaint { Color = dots[i], IsAntialias = true };
            canvas.DrawCircle(240 + (i * 26), 150, 9, dot);
        }

        return surface.Snapshot();
    }

    /// <summary>The sample under one style, as a bitmap the interface can show.</summary>
    private static Bitmap RenderSample(ExportStyle style)
    {
        using var sample = SampleShot();
        using var document = new Document(sample, new CaptureRect(0, 0, 320, 200))
        {
            Selection = new CaptureRect(0, 0, 320, 200),
        };

        using var rendered = DocumentRenderer.Render(document, style);
        using var data = rendered.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = new MemoryStream(data.ToArray());

        return new Bitmap(stream);
    }

    private void StoreSave(Func<SaveOptions, SaveOptions> change) =>
        Store(_settings with { Save = change(_settings.Save ?? SaveOptions.Default) });

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

    /// <summary>
    /// Something was changed. Raised on every edit rather than on close: this
    /// window is left open while the next screenshot is taken, and a setting
    /// that waits for the window to shut is a setting that did not work.
    /// </summary>
    public event EventHandler<AppSettings>? Changed;

    private void Store(AppSettings settings)
    {
        _settings = settings;
        Changed?.Invoke(this, settings);

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
