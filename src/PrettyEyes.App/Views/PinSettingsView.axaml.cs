using System.Globalization;
using Avalonia.Controls;
using Controls = PrettyEyes.App.Controls;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// Everything about pinning, in the card that opens from its own icon.
///
/// The settings ride with the feature rather than waiting for the general move
/// of the old sections: without this card pinning would ship with three
/// hotkeys, two switches and nowhere at all to reach them.
/// </summary>
public partial class PinSettingsView : UserControl
{
    /// <summary>
    /// Below this a pinned window is an invisible trap for clicks. The same
    /// floor the window itself keeps, said in one more place because a settings
    /// field that can set an illegal value is a bug waiting for a Tuesday.
    /// </summary>
    private const double MinOpacity = 0.2;

    private const double Step = 0.05;

    private bool _loading;
    private double _opacity = 1;

    public PinSettingsView()
    {
        InitializeComponent();

        LessOpaque.Click += (_, _) => Nudge(-Step);
        MoreOpaque.Click += (_, _) => Nudge(Step);

        DrawOnPinned.IsCheckedChanged += (_, _) => Changed?.Invoke(
            this,
            settings => settings with { DrawOnPinned = DrawOnPinned.IsChecked == true });

        HideOnCapture.IsCheckedChanged += (_, _) => Changed?.Invoke(
            this,
            settings => settings with { HidePinnedOnCapture = HideOnCapture.IsChecked == true });

        PinHotkey.HotkeyChanged += (_, hotkey) => HotkeyChanged?.Invoke(this, (HotkeyAction.Pin, hotkey));
        HidePinnedHotkey.HotkeyChanged += (_, hotkey) =>
            HotkeyChanged?.Invoke(this, (HotkeyAction.HidePinned, hotkey));
        ShowPinnedHotkey.HotkeyChanged += (_, hotkey) =>
            HotkeyChanged?.Invoke(this, (HotkeyAction.ShowPinned, hotkey));
    }

    /// <summary>
    /// One of the switches moved. A function rather than a finished record: the
    /// settings window owns the current one and is the only place allowed to
    /// replace it.
    /// </summary>
    public event EventHandler<Func<AppSettings, AppSettings>>? Changed;

    /// <summary>A combination was typed into one of the three fields.</summary>
    public event EventHandler<(HotkeyAction Action, HotkeyDefinition Hotkey)>? HotkeyChanged;

    /// <summary>
    /// The field one action is typed into. Handed out so the settings window
    /// can put a refused combination back where it came from.
    /// </summary>
    public Controls.HotkeyBox? Field(HotkeyAction action) => action switch
    {
        HotkeyAction.Pin => PinHotkey,
        HotkeyAction.HidePinned => HidePinnedHotkey,
        HotkeyAction.ShowPinned => ShowPinnedHotkey,
        _ => null,
    };

    /// <summary>Says why a combination was refused, right here in the card.</summary>
    public void Warn(string? text)
    {
        Message.Text = text ?? string.Empty;
        Message.IsVisible = text is not null;
    }

    public void Load(AppSettings settings)
    {
        _loading = true;
        Warn(null);

        _opacity = Math.Clamp(settings.PinOpacity, MinOpacity, 1);
        ShowOpacity();

        DrawOnPinned.IsChecked = settings.DrawOnPinned;
        HideOnCapture.IsChecked = settings.HidePinnedOnCapture;

        // Unassigned by default, and shown as such rather than as some
        // combination nobody chose.
        PinHotkey.Value = settings.PinHotkey ?? HotkeyDefinition.None;
        HidePinnedHotkey.Value = settings.HidePinnedHotkey ?? HotkeyDefinition.None;
        ShowPinnedHotkey.Value = settings.ShowPinnedHotkey ?? HotkeyDefinition.None;

        _loading = false;
    }

    private void Nudge(double step)
    {
        // Rounded, or the file ends up with 0.7999999999999998 and every
        // reader of it has to wonder what that means.
        _opacity = Math.Round(Math.Clamp(_opacity + step, MinOpacity, 1), 2);
        ShowOpacity();

        if (_loading)
        {
            return;
        }

        var wanted = _opacity;

        Changed?.Invoke(this, settings => settings with { PinOpacity = wanted });
    }

    private void ShowOpacity() =>
        OpacityValue.Text = Math.Round(_opacity * 100).ToString(CultureInfo.InvariantCulture) + " %";
}
