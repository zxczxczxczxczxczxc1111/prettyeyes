using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PrettyEyes.App.Services;

/// <summary>
/// Invisible one-pixel window that exists so the tray-only app still has a
/// TopLevel. Clipboard, the save dialog and the hotkey message loop all need
/// one, and Avalonia does not hand them out otherwise.
/// </summary>
public partial class HostWindow : Window
{
    public HostWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Shows the window off-screen so a native handle exists without the user
    /// ever seeing it.
    /// </summary>
    public void ShowHidden()
    {
        Show();

        // Position only sticks once the native window exists, so it is set
        // after Show, not before.
        Position = new Avalonia.PixelPoint(-32000, -32000);
    }
}
