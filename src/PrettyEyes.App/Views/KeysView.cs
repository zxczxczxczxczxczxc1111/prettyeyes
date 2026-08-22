using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PrettyEyes.App.Controls;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// Every shortcut in one place, shown in the modal sheet.
///
/// Exists because the application grew more gestures than anyone would guess
/// at: the wheel means three different things depending on the modifier, and
/// Escape walks down a ladder rather than closing something. None of that is
/// discoverable by poking at it.
///
/// The configurable ones are read from the settings rather than written into
/// the text, because a list that says Alt + G to somebody who changed it to
/// something else is worse than no list at all.
/// </summary>
public sealed class KeysView : UserControl
{
    private readonly StackPanel _rows = new() { Spacing = 18 };

    public KeysView()
    {
        Width = 420;

        Content = new ScrollViewer
        {
            MaxHeight = 460,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _rows,
        };
    }

    public void Load(AppSettings settings)
    {
        _rows.Children.Clear();

        var shot = new List<(string Keys, string What)>
        {
            (HotkeyBox.Describe(settings.Hotkey), "выделить область"),
            (HotkeyBox.Describe(settings.FullScreenHotkey), "весь монитор в буфер"),
        };

        // Only when they have been given a key: an empty line reads as a
        // feature that is broken rather than as one that is switched off.
        Add(shot, settings.PinHotkey, "закрепить снимок");
        Add(shot, settings.HidePinnedHotkey, "скрыть закреплённые");
        Add(shot, settings.ShowPinnedHotkey, "показать закреплённые");

        Section("Снимок", shot);

        Section("В окне выделения",
        [
            ("Enter, Ctrl + C", "копировать в буфер"),
            ("Ctrl + S", "сохранить в файл"),
            ("Ctrl + Z", "отменить последнее"),
            ("C", "цвет инструмента"),
            ("Стрелки", "сдвинуть выделение на пиксель, с Shift быстрее"),
            ("Ctrl или Alt с колесом", "размер объекта под курсором"),
            ("Esc", "закрыть карточку, бросить объект, снять инструмент, закрыть окно"),
        ]);

        Section("В закреплённом окне",
        [
            ("Колесо", "масштаб"),
            ("Ctrl с колесом", "прозрачность"),
            ("Alt с колесом", "размер объекта под курсором"),
            ("Пробел", "держать, чтобы двигать окно, а не рисовать"),
            ("Ctrl + C, Ctrl + S", "копировать, сохранить"),
            ("Ctrl + Z", "отменить последнее"),
            ("Esc", "закрыть карточку, бросить объект, снять инструмент; окно не закрывает"),
        ]);

        Section("При наборе текста",
        [
            ("Ctrl + Enter", "закончить надпись"),
            ("Enter", "новая строка"),
            ("Ctrl + A", "выделить всё"),
            ("Ctrl + V", "вставить"),
        ]);
    }

    private static void Add(
        List<(string Keys, string What)> into,
        Core.Platform.HotkeyDefinition? hotkey,
        string what)
    {
        if (hotkey is { Assigned: true })
        {
            into.Add((HotkeyBox.Describe(hotkey), what));
        }
    }

    private void Section(string title, IReadOnlyList<(string Keys, string What)> rows)
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = (IBrush?)Application.Current?.FindResource("TextDim"),
        });

        foreach (var (keys, what) in rows)
        {
            var line = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("150,12,*"),
            };

            var name = new TextBlock
            {
                Text = keys,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };

            var meaning = new TextBlock
            {
                Text = what,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (IBrush?)Application.Current?.FindResource("TextDim"),
            };

            Grid.SetColumn(meaning, 2);
            line.Children.Add(name);
            line.Children.Add(meaning);
            panel.Children.Add(line);
        }

        _rows.Children.Add(panel);
    }
}
