using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PrettyEyes.App.Views;

/// <summary>
/// A layer over the settings window, not a second window.
///
/// ShowDialog does not appear anywhere in this project, and a modal window
/// would arrive with its own decorations, its own placement and its own taskbar
/// entry - three things the settings window spent effort not having. This is a
/// dimmed sheet with a card on it, living inside the same window.
/// </summary>
public sealed class SettingsModal : Panel
{
    private readonly Border _card;
    private readonly TextBlock _title;
    private readonly ContentControl _body;

    public SettingsModal()
    {
        IsVisible = false;

        // The dim sheet. Hit-testable on purpose: it swallows clicks meant for
        // the settings underneath, which is what makes the thing modal at all.
        Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));

        _title = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        _title.Classes.Add("modaltitle");

        var close = new Button { Content = "✕", Margin = new Thickness(0, 0, 8, 0) };
        close.Classes.Add("ghost");
        close.Classes.Add("close");
        close.Click += (_, _) => Close();

        var bar = new Grid
        {
            Height = 38,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        bar.Children.Add(_title);
        Grid.SetColumn(close, 1);
        bar.Children.Add(close);

        _body = new ContentControl { Margin = new Thickness(14, 0, 14, 14) };

        var stack = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        stack.Children.Add(bar);
        stack.Children.Add(_body);

        _card = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 260,
            Child = stack,
        };
        _card.Classes.Add("modalcard");

        Children.Add(_card);

        // A click on the sheet closes; a click that started inside the card is
        // somebody using the card, not leaving it.
        PointerPressed += (_, e) =>
        {
            if (e.Source is Visual source && !IsInsideCard(source))
            {
                Close();
            }
        };
    }

    public bool IsOpen => IsVisible;

    private bool IsInsideCard(Visual source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _card))
            {
                return true;
            }
        }

        return false;
    }

    public void Show(Control content, string title)
    {
        _title.Text = title;
        _body.Content = content;
        IsVisible = true;

        // Focus has to come along, otherwise Esc keeps talking to whatever the
        // settings window had focused before the sheet went up.
        Focus();
    }

    public void Close()
    {
        IsVisible = false;

        // The content is handed back out: the same ToolStylePopup instance gets
        // shown again next time, and a control cannot have two parents.
        _body.Content = null;
    }
}
