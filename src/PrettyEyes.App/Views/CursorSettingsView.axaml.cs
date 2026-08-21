using Avalonia.Controls;
using Avalonia.Media;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// Which crosshair the overlay draws. Behind the cursor icon now: the icon was
/// already in the settings, and a section saying the same thing was a second
/// place to keep in step.
/// </summary>
public partial class CursorSettingsView : UserControl
{
    private const string ActiveClass = "active";

    private CursorStyle _chosen = CursorStyle.Cross;

    public CursorSettingsView()
    {
        InitializeComponent();

        foreach (var style in Crosshair.All)
        {
            var button = new Button
            {
                Tag = style,
                Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(Crosshair.Icon(style)) },
            };

            button.Classes.Add("toolpick");
            ToolTip.SetTip(button, Label(style));
            button.Click += (sender, _) =>
            {
                if (sender is Button picked && picked.Tag is CursorStyle wanted)
                {
                    Pick(wanted);
                }
            };

            CursorRow.Children.Add(button);
        }
    }

    /// <summary>A function rather than a record: the window owns the settings.</summary>
    public event EventHandler<Func<AppSettings, AppSettings>>? Changed;

    public void Load(AppSettings settings)
    {
        _chosen = settings.Cursor;
        Show();
    }

    /// <summary>The label a hover shows. There is no room for five captions.</summary>
    public static string Label(CursorStyle style) => style switch
    {
        CursorStyle.Cross => "Крест",
        CursorStyle.Gap => "Крест с просветом",
        CursorStyle.Dot => "Точка",
        CursorStyle.Scope => "Прицел",
        _ => "Стрелка",
    };

    private void Pick(CursorStyle style)
    {
        _chosen = style;
        Show();

        Changed?.Invoke(this, settings => settings with { Cursor = style });
    }

    private void Show()
    {
        foreach (var child in CursorRow.Children)
        {
            if (child is not Button button || button.Tag is not CursorStyle style)
            {
                continue;
            }

            button.Classes.Remove(ActiveClass);

            if (style == _chosen)
            {
                button.Classes.Add(ActiveClass);
            }
        }
    }
}
