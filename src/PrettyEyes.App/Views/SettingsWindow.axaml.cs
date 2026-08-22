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

    private AppServices? _services;
    private bool _resetAsked;

    private AppSettings _settings = AppSettings.Default;
    private ToolVisibility _tools = new();
    private ToolStyles _styles = new();
    private ExportStyle _export = ExportStyle.None;

    /// <summary>
    /// One instance, reused: the card is the overlay's own control, and building
    /// a fresh one per right click would rebuild the whole palette each time.
    /// </summary>
    private readonly ToolStylePopup _stylePopup = new();

    /// <summary>
    /// Pinning has settings of its own and no old section to live in, so its
    /// card is built here next to the style card rather than waiting for the
    /// general move of the other sections.
    /// </summary>
    private readonly PinSettingsView _pinSettings = new();

    /// <summary>
    /// The old sections, each now behind its own icon. Built once and reused:
    /// the modal hands its content back on close, and a control cannot have two
    /// parents.
    /// </summary>
    private readonly MagnifierSettingsView _magnifierSettings = new();
    private readonly CursorSettingsView _cursorSettings = new();
    private readonly QuickSaveSettingsView _quickSaveSettings = new();
    private readonly ExportSettingsView _exportSettings = new();

    /// <summary>The list of shortcuts, built once and shown in the modal.</summary>
    private readonly KeysView _keys = new();
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

        _pinSettings.Changed += (_, change) => Store(change(_settings));
        _magnifierSettings.Changed += (_, change) =>
        {
            Store(change(_settings));
            ShowToolRow();
        };

        _cursorSettings.Changed += (_, change) => Store(change(_settings));

        _quickSaveSettings.Changed += (_, change) => StoreSave(change);
        _quickSaveSettings.Failed += (_, text) => ShowFailure(text);
        _quickSaveSettings.Toggled += (_, _) => ShowToolRow();

        _exportSettings.Changed += (_, style) =>
        {
            _export = style;
            Store(_settings with { Export = style });
            ShowToolRow();
        };
        _pinSettings.HotkeyChanged += (_, typed) => Apply(typed.Action, typed.Hotkey);

        _stylePopup.StyleChanged += (_, change) =>
        {
            _styles.Set(change.Kind, change.Style);
            Store(_settings with { ToolStyles = new Dictionary<ToolKind, ToolStyle>(_styles.Stored) });
        };

        // Esc closes the sheet first and the window second: the other order
        // would throw away the window while a card is still open on it.
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && Modal.IsOpen)
            {
                Modal.Close();
                e.Handled = true;
            }
        };

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

        _tools = new ToolVisibility(settings.Tools);
        _styles = new ToolStyles(settings.ToolStyles ?? []);
        BuildToolRow();

        CheckUpdates.IsChecked = settings.CheckUpdates;
        CurrentVersion.Text = $"установлена {UpdateService.Current}";
        ShowUpdateState(updates.State);

        _export = settings.Export ?? ExportStyle.None;
        _loading = false;

        RegionHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.Region, hotkey);
        FullScreenHotkey.HotkeyChanged += (_, hotkey) => Apply(HotkeyAction.FullScreen, hotkey);

        // What the key costs elsewhere, and why one could not be taken. Both
        // used to be silence: the field simply did not react, which reads as
        // broken rather than as refused.
        foreach (var box in new[] { RegionHotkey, FullScreenHotkey })
        {
            box.Warned += (_, cost) => ShowCost(cost);
            box.Refused += (_, why) => ShowWarning(why);
        }
        Autostart.IsCheckedChanged += OnAutostartChanged;
        CheckUpdates.IsCheckedChanged += OnCheckUpdatesChanged;
        CheckNow.Click += (_, _) => _ = updates.CheckAsync(manual: true);
        OpenReleases.Click += (_, _) => OpenPage(GitHubUpdateSource.ReleasesPage);
        InstallUpdate.Click += (_, _) => _ = _install?.Invoke();

        // Unsubscribed on close: the service outlives this window, and a
        // handler pointing at a closed one would write into nothing.
        updates.StateChanged += OnUpdateState;
        Closed += (_, _) => updates.StateChanged -= OnUpdateState;


        if (!regionRegistered || !fullScreenRegistered)
        {
            ShowWarning("Комбинация занята другой программой. Выбери другую.");
        }
    }


    /// <summary>
    /// Fills the state block and wires the three buttons under it.
    ///
    /// Apart from Configure on purpose: everything here is read once and shown,
    /// nothing here is a setting, and the argument list of Configure is long
    /// enough already.
    /// </summary>
    public void ShowState(AppServices services)
    {
        _services = services;

        foreach (var (text, detail) in StateLines(services))
        {
            var line = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (IBrush?)Application.Current?.FindResource("TextDim"),
            };

            if (detail is not null)
            {
                // The technical answer is one hover away and out of the way:
                // the name of a capture API means nothing to the person using
                // the application and everything to whoever gets asked about
                // it later.
                ToolTip.SetTip(line, detail);
            }

            Health.Children.Add(line);
        }

        OpenLog.Click += (_, _) => OpenPage(Log.DefaultPath);
        OpenShots.Click += (_, _) => OpenShotsFolder(services);
        ResetSettings.Click += (_, _) => AskThenReset();
        ShowKeys.Click += (_, _) =>
        {
            // Loaded on opening rather than once: hotkeys can be changed in
            // the left column of this very window while the list sits here.
            _keys.Load(_settings);
            Modal.Show(_keys, "Сочетания клавиш");
        };
    }

    /// <summary>
    /// The lines under "Состояние", each with the technical detail that
    /// belongs in a tooltip rather than on the page.
    /// </summary>
    private static IEnumerable<(string Text, string? Detail)> StateLines(AppServices services)
    {
        if (services.Capture is DesktopCapture capture && capture.Painters.Count > 0)
        {
            yield return CaptureHealth(capture.Painters);

            if (capture.LastMilliseconds > 0)
            {
                yield return ($"Последний снимок: {capture.LastMilliseconds:F0} мс", null);
            }
        }

        var shots = services.Shots.Current;

        yield return shots.Total == 0
            ? ("Снимков пока нет", null)
            : ($"Снимков: {shots.Total}, за неделю {services.Shots.ThisWeek}", null);

        if (shots.Total > 0)
        {
            yield return ($"Буфер {shots.ToClipboard}, файл {shots.ToFile}, закреплено {shots.ToPin}", null);
        }

        yield return services.Settings.Save?.Ready == true
            ? ($"Быстрое сохранение: {services.Settings.Save.Folder}", null)
            : ("Быстрое сохранение выключено", null);
    }

    /// <summary>
    /// Says what the person will notice, not which API took the picture.
    ///
    /// Nobody outside this repository knows what output duplication is, and
    /// the line was written for its author rather than for its reader. What
    /// matters to a reader is whether Windows will draw its yellow frame and
    /// whether video will come out black, so that is what it says now.
    /// </summary>
    private static (string Text, string? Detail) CaptureHealth(IReadOnlyDictionary<string, string> painters)
    {
        var detail = string.Join(", ", painters
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{Short(pair.Key)}: {pair.Value}"));

        var plain = painters.Values.Where(name => name == "GDI").ToArray();
        var system = painters.Values.Where(name => name.StartsWith("Windows.", StringComparison.Ordinal)).ToArray();

        if (plain.Length > 0)
        {
            return ("Захват: запасной способ, видео и игры выйдут чёрными", detail);
        }

        if (system.Length > 0)
        {
            return ("Захват: запасной способ, по краю экрана бывает жёлтая рамка", detail);
        }

        return ("Захват: в порядке", detail);
    }

    /// <summary>
    /// A display arrives named as a path, with leading slashes and a dot. On
    /// this line only the tail of that means anything to a person.
    /// </summary>
    private static string Short(string device) => device.Trim('\\', '.');

    private void OpenShotsFolder(AppServices services)
    {
        var folder = services.Settings.Save?.Folder;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            ShowWarning("Папка не выбрана. Включи быстрое сохранение в настройках функции.");

            return;
        }

        OpenPage(folder);
    }

    /// <summary>
    /// Two clicks, not a dialog: the button itself asks, and forgets the
    /// question if nobody answers. A modal sheet for one irreversible button
    /// would be more ceremony than the button deserves, and a silent reset
    /// would be worse than either.
    /// </summary>
    private void AskThenReset()
    {
        if (!_resetAsked)
        {
            _resetAsked = true;
            ResetSettings.Content = "Точно сбросить?";

            DispatcherTimer.RunOnce(
                () =>
                {
                    _resetAsked = false;
                    ResetSettings.Content = "Сбросить";
                },
                TimeSpan.FromSeconds(4));

            return;
        }

        Store(AppSettings.Default);

        // Closed rather than refilled: every field, checkbox and icon on this
        // window was built from the old settings, and the window is rebuilt
        // from scratch the next time it opens anyway.
        Close();
    }

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
                ToolRow.Children.Add(WithCorner(button, feature));
            }
            else
            {
                button.Click += OnFeatureToggled;
                FeatureRow.Children.Add(WithCorner(button, feature));
            }
        }

        BuildDefaultToolRow();
        ShowToolRow();
    }

    /// <summary>
    /// The tool a capture starts with. "Не выбран" comes first and is the
    /// default: a screenshot tool that arms a pen before anyone asked is a
    /// screenshot tool that draws by accident.
    /// </summary>
    private void BuildDefaultToolRow()
    {
        DefaultToolRow.Children.Clear();

        // A cross rather than the words: in a row of icons a word is wider than
        // all of them and reads as a caption for the row instead of one of its
        // choices. The words stay in the tooltip - a cross means "none" to most
        // people, not to everybody.
        var none = new Button
        {
            Tag = null,
            Content = new Avalonia.Controls.Shapes.Path
            {
                Data = Avalonia.Media.Geometry.Parse("M4.5,4.5 L11.5,11.5 M11.5,4.5 L4.5,11.5"),
            },
        };

        none.Classes.Add("toolpick");
        ToolTip.SetTip(none, "Не выбран");
        none.Click += (_, _) => PickDefaultTool(null);
        DefaultToolRow.Children.Add(none);

        foreach (var tool in ConfigurableFeature.DefaultToolChoices)
        {
            var button = new Button
            {
                Tag = tool,
                Content = new Avalonia.Controls.Shapes.Path
                {
                    Data = Avalonia.Media.Geometry.Parse(ToolIcon(tool)),
                },
            };

            button.Classes.Add("toolpick");
            ToolTip.SetTip(button, ToolName(tool));
            button.Click += (_, _) => PickDefaultTool(tool);

            DefaultToolRow.Children.Add(button);
        }

        ShowDefaultToolRow();
    }

    private void ShowDefaultToolRow()
    {
        foreach (var child in DefaultToolRow.Children)
        {
            if (child is not Button button)
            {
                continue;
            }

            button.Classes.Remove("active");

            if ((button.Tag as ToolKind?) == _settings.DefaultTool)
            {
                button.Classes.Add("active");
            }
        }
    }

    private void PickDefaultTool(ToolKind? tool)
    {
        if (_loading)
        {
            return;
        }

        Store(_settings with { DefaultTool = tool });
        ShowDefaultToolRow();
    }

    /// <summary>
    /// One square in either row: the toggle itself, and over its bottom right
    /// corner a second, tiny button that opens the settings of that feature.
    ///
    /// The corner is the whole point of the mark. The right mouse button opens
    /// the same thing, but a button nobody can see is not an affordance, and
    /// the keyboard has to be able to get there too - which a right click
    /// cannot offer at all.
    /// </summary>
    private Button NewFeatureButton(ConfigurableFeature feature)
    {
        var button = new Button
        {
            Tag = feature,
            Content = new Avalonia.Controls.Shapes.Path
            {
                Data = Avalonia.Media.Geometry.Parse(FeatureIcon(feature.Id)),
            },
        };

        button.Classes.Add("toolpick");
        ToolTip.SetTip(button, FeatureName(feature.Id));

        if (feature.HasSettings)
        {
            button.AddHandler(
                PointerPressedEvent,
                (_, e) =>
                {
                    if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed)
                    {
                        OpenFeatureSettings(feature);
                        e.Handled = true;
                    }
                },
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        return button;
    }

    /// <summary>
    /// The square and its corner, stacked. Two overlapping controls rather than
    /// one: a corner drawn inside the button would be a picture of a button,
    /// and clicking it would toggle the feature off instead of opening it.
    /// </summary>
    private Control WithCorner(Button square, ConfigurableFeature feature)
    {
        if (!feature.HasSettings)
        {
            return square;
        }

        var corner = new Button
        {
            Content = new Avalonia.Controls.Shapes.Path
            {
                Data = Avalonia.Media.Geometry.Parse("M0,6 L6,6 L6,0 Z"),
            },
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 2),
        };

        corner.Classes.Add("corner");
        ToolTip.SetTip(corner, "Настройки: " + FeatureName(feature.Id));
        corner.Click += (_, e) =>
        {
            OpenFeatureSettings(feature);
            e.Handled = true;
        };

        var stack = new Panel();
        stack.Children.Add(square);
        stack.Children.Add(corner);

        return stack;
    }

    /// <summary>
    /// What the right button, and the corner, open. Drawing tools get the same
    /// colour and thickness card the overlay uses; the features get theirs when
    /// their sections move in.
    /// </summary>
    private void OpenFeatureSettings(ConfigurableFeature feature)
    {
        if (feature.Id == FeatureId.Pin)
        {
            _pinSettings.Load(_settings);
            Modal.Show(_pinSettings, FeatureName(feature.Id));

            return;
        }

        if (feature.Id == FeatureId.Magnifier)
        {
            _magnifierSettings.Load(_settings);
            Modal.Show(_magnifierSettings, FeatureName(feature.Id));

            return;
        }

        if (feature.Id == FeatureId.Cursor)
        {
            _cursorSettings.Load(_settings);
            Modal.Show(_cursorSettings, FeatureName(feature.Id));

            return;
        }

        if (feature.Id == FeatureId.QuickSave)
        {
            _quickSaveSettings.Load(_settings.Save ?? SaveOptions.Default);
            Modal.Show(_quickSaveSettings, FeatureName(feature.Id));

            return;
        }

        if (feature.Id == FeatureId.Export)
        {
            _exportSettings.Load(_export);
            Modal.Show(_exportSettings, FeatureName(feature.Id));

            return;
        }

        if (feature.Tool is not { } tool)
        {
            return;
        }

        _stylePopup.Open(tool, _styles.For(tool));
        Modal.Show(_stylePopup, FeatureName(feature.Id));
    }

    /// <summary>Which of them are on, right now.</summary>
    private void ShowToolRow()
    {
        foreach (var child in ToolRow.Children.Concat(FeatureRow.Children))
        {
            // The square is either the child itself or the first thing in the
            // little stack that also holds its corner.
            var button = child as Button
                ?? (child as Panel)?.Children.OfType<Button>().FirstOrDefault();

            if (button?.Tag is not ConfigurableFeature feature)
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
                break;

            case FeatureId.QuickSave:
                var enabled = !(_settings.Save ?? SaveOptions.Default).Enabled;
                _quickSaveSettings.ShowEnabled(enabled);
                StoreSave(save => save with { Enabled = enabled });
                break;

            case FeatureId.Export:
                _export = _export with { Enabled = !_export.Enabled };
                Store(_settings with { Export = _export });
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

        // A hidden tool cannot be the one every capture starts with. Reset only
        // after TrySet said yes: the refusal path leaves everything as it was.
        var settings = _settings with { Tools = _tools.ToDictionary() };

        if (!_tools.IsShown(kind) && settings.DefaultTool == kind)
        {
            settings = settings with { DefaultTool = null };
        }

        Store(settings);
        ShowDefaultToolRow();
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
        ToolKind.Text => "M4,4.5 H12 M8,4.5 V12 M6,12 H10",
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
        ToolKind.Text => "Надпись",
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
        FeatureId.Text => ToolIcon(ToolKind.Text),
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
        FeatureId.Text => ToolName(ToolKind.Text),
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
            ShowWarning("Эта комбинация уже занята другим хоткеем. Оставил прежнюю.");
            return;
        }

        // Clearing one is not a registration: there is nothing to give to the
        // system, and asking it for virtual key zero would fail and look like
        // "the combination is busy".
        if (!hotkey.Assigned)
        {
            _hotkeys.Unregister(action);
            HideMessage();
            Store(With(action, hotkey));

            return;
        }

        if (_hotkeys.TryRegister(action, hotkey))
        {
            HideMessage();
            Store(With(action, hotkey));

            return;
        }

        // Put the working combination back so the user is never left without one.
        if (Existing(action) is { Assigned: true } previous)
        {
            _hotkeys.TryRegister(action, previous);
        }
        Restore(action);
        ShowWarning($"{HotkeyBox.Busy(hotkey)} Оставил прежнюю.");
    }

    /// <summary>
    /// Our own hotkeys must not fight over one combination. A table rather than
    /// a fork: with five actions "Region or else FullScreen" stops being a
    /// sentence anybody can read.
    /// </summary>
    private bool Taken(HotkeyAction action, HotkeyDefinition hotkey) =>
        hotkey.Assigned
        && Actions()
            .Where(other => other != action)
            .Any(other => Existing(other) is { Assigned: true } taken && taken == hotkey);

    private static IEnumerable<HotkeyAction> Actions() =>
        Enum.GetValues<HotkeyAction>();

    /// <summary>What is currently assigned to an action.</summary>
    private HotkeyDefinition Existing(HotkeyAction action) => action switch
    {
        HotkeyAction.Region => _settings.Hotkey,
        HotkeyAction.FullScreen => _settings.FullScreenHotkey,
        HotkeyAction.Pin => _settings.PinHotkey ?? HotkeyDefinition.None,
        HotkeyAction.HidePinned => _settings.HidePinnedHotkey ?? HotkeyDefinition.None,
        HotkeyAction.ShowPinned => _settings.ShowPinnedHotkey ?? HotkeyDefinition.None,
        _ => HotkeyDefinition.None,
    };

    /// <summary>The same settings with one action's combination replaced.</summary>
    private AppSettings With(HotkeyAction action, HotkeyDefinition hotkey) => action switch
    {
        HotkeyAction.Region => _settings with { Hotkey = hotkey },
        HotkeyAction.FullScreen => _settings with { FullScreenHotkey = hotkey },
        HotkeyAction.Pin => _settings with { PinHotkey = hotkey },
        HotkeyAction.HidePinned => _settings with { HidePinnedHotkey = hotkey },
        HotkeyAction.ShowPinned => _settings with { ShowPinnedHotkey = hotkey },
        _ => _settings,
    };

    /// <summary>Puts the field back to what is actually registered.</summary>
    private void Restore(HotkeyAction action)
    {
        _loading = true;

        var field = Field(action);

        if (field is not null)
        {
            field.Value = Existing(action);
        }

        _loading = false;
    }

    /// <summary>
    /// The box an action is typed into, or null when its card is not open: the
    /// three pinning fields live in a modal that exists only while it is shown.
    /// </summary>
    private HotkeyBox? Field(HotkeyAction action) => action switch
    {
        HotkeyAction.Region => RegionHotkey,
        HotkeyAction.FullScreen => FullScreenHotkey,
        _ => _pinSettings.Field(action),
    };



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

    /// <summary>
    /// A combination was taken and it costs something. Not a refusal and not a
    /// question: the choice is the user's, this only says what it bought.
    /// </summary>
    private void ShowCost(string? cost)
    {
        if (cost is null)
        {
            HideMessage();
            _pinSettings.Warn(null);

            return;
        }

        ShowWarning(cost);
    }

    private void ShowWarning(string text) => Show(text, "Warn");

    private void ShowFailure(string text) => Show(text, "Danger");

    private void Show(string text, string brushKey)
    {
        // The card covers the page and dims it; the line below would be talking
        // to a wall.
        if (Modal.IsOpen)
        {
            _pinSettings.Warn(text);
        }

        Message.Text = text;
        Message.Foreground = (IBrush)Application.Current!.FindResource(brushKey)!;
        Message.IsVisible = true;
    }

    private void HideMessage() => Message.IsVisible = false;
}
