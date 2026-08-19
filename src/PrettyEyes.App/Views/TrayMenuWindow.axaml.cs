using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Views;

/// <summary>
/// The tray menu as an ordinary window of ours. Windows draws a NativeMenu
/// itself, which means no font, no colours and no animation - everything the
/// rest of the app has.
/// </summary>
public partial class TrayMenuWindow : Window
{
    private const int Gap = 8;

    private bool _focused;

    public TrayMenuWindow()
    {
        InitializeComponent();

        CaptureItem.Click += (_, _) => Pick(TrayMenuChoice.Capture);
        SettingsItem.Click += (_, _) => Pick(TrayMenuChoice.Settings);
        ExitItem.Click += (_, _) => Pick(TrayMenuChoice.Exit);

        // A menu that outlives its click is not a menu - but the rule only
        // starts once it has actually been focused, otherwise it closes itself
        // in the same frame it opens.
        Activated += (_, _) => _focused = true;
        Deactivated += (_, _) =>
        {
            if (_focused)
            {
                Close();
            }
        };
        Opened += (_, _) => WindowCorners.Round(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
    }

    public event EventHandler<TrayMenuChoice>? Picked;

    /// <summary>
    /// Opens above and to the left of the cursor, the way a tray menu does,
    /// and stays inside the monitor it was opened on.
    /// </summary>
    public void ShowAt(int cursorX, int cursorY, CaptureRect monitor, double scale)
    {
        Show();

        // The size is only known after the first layout pass, so the placement
        // waits for it: a menu positioned before that lands off-screen.
        Dispatcher.UIThread.Post(
            () =>
            {
                var width = (int)Math.Round(ClientSize.Width * scale);
                var height = (int)Math.Round(ClientSize.Height * scale);

                var x = Math.Clamp(cursorX - width, monitor.X, Math.Max(monitor.X, monitor.Right - width));
                var y = Math.Clamp(cursorY - height - Gap, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - height));

                Position = new PixelPoint(x, y);
                Activate();

                Card.Opacity = 1;
                Card.RenderTransform = TransformOperations.Parse("translateY(0px)");
            },
            DispatcherPriority.Loaded);
    }

    private void Pick(TrayMenuChoice choice)
    {
        Picked?.Invoke(this, choice);
        Close();
    }
}
