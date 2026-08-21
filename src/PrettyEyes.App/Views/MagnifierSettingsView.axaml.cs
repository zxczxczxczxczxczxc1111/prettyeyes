using Avalonia.Controls;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// Off, on, on with the grid. Moved out of the page and into the card that
/// opens from the magnifier's own icon: the icon was already there, and the
/// same setting in two places is two places to keep in step.
/// </summary>
public partial class MagnifierSettingsView : UserControl
{
    private const string ActiveClass = "active";

    private AppSettings _settings = AppSettings.Default;

    public MagnifierSettingsView()
    {
        InitializeComponent();

        MagnifierOff.Click += (_, _) => Pick(shown: false, grid: _settings.MagnifierGrid);
        MagnifierPlain.Click += (_, _) => Pick(shown: true, grid: false);
        MagnifierWithGrid.Click += (_, _) => Pick(shown: true, grid: true);
    }

    /// <summary>
    /// A function rather than a finished record: the settings window owns the
    /// current one and is the only place allowed to replace it.
    /// </summary>
    public event EventHandler<Func<AppSettings, AppSettings>>? Changed;

    public void Load(AppSettings settings)
    {
        _settings = settings;
        Show();
    }

    /// <summary>
    /// Turning the magnifier off keeps whatever grid choice was made: coming
    /// back should land where it was left, not on a default.
    /// </summary>
    private void Pick(bool shown, bool grid)
    {
        _settings = _settings with { ShowMagnifier = shown, MagnifierGrid = grid };
        Show();

        Changed?.Invoke(this, settings => settings with { ShowMagnifier = shown, MagnifierGrid = grid });
    }

    private void Show()
    {
        foreach (var (button, on) in new[]
        {
            (MagnifierOff, !_settings.ShowMagnifier),
            (MagnifierPlain, _settings.ShowMagnifier && !_settings.MagnifierGrid),
            (MagnifierWithGrid, _settings.ShowMagnifier && _settings.MagnifierGrid),
        })
        {
            button.Classes.Remove(ActiveClass);

            if (on)
            {
                button.Classes.Add(ActiveClass);
            }
        }
    }
}
