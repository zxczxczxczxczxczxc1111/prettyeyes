using Avalonia.Controls;
using Controls = PrettyEyes.App.Controls;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.App.Views;

/// <summary>
/// The pointer's card: the combination that turns the over-the-screen mode on,
/// and the two things about that mode which the button cannot say by itself.
///
/// Its own card rather than a field on the page because the combination arrives
/// unassigned, like the three pinning ones, and an empty field on the page is
/// one more thing to explain to somebody who will never demonstrate anything.
/// </summary>
public partial class LaserSettingsView : UserControl
{
    public LaserSettingsView()
    {
        InitializeComponent();

        LaserHotkey.Warned += (_, cost) => Warn(cost);
        LaserHotkey.Refused += (_, why) => Warn(why);

        LaserHotkey.HotkeyChanged += (_, hotkey) =>
            HotkeyChanged?.Invoke(this, (HotkeyAction.Laser, hotkey));
    }

    /// <summary>A combination was typed into the field.</summary>
    public event EventHandler<(HotkeyAction Action, HotkeyDefinition Hotkey)>? HotkeyChanged;

    /// <summary>
    /// The field one action is typed into. Handed out so the settings window
    /// can put a refused combination back where it came from.
    /// </summary>
    public Controls.HotkeyBox? Field(HotkeyAction action) =>
        action == HotkeyAction.Laser ? LaserHotkey : null;

    /// <summary>Says why a combination was refused, right here in the card.</summary>
    public void Warn(string? text)
    {
        Message.Text = text ?? string.Empty;
        Message.IsVisible = text is not null;
    }

    public void Load(AppSettings settings)
    {
        Warn(null);

        // Unassigned by default, and shown as such rather than as some
        // combination nobody chose.
        LaserHotkey.Value = settings.LaserHotkey ?? HotkeyDefinition.None;
    }
}
