using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Views;

/// <summary>
/// The one sign that a whole-monitor shot happened.
///
/// Until now the only answer was a tray notification: it arrives late, it can
/// be switched off system-wide, and it lives in a corner nobody watches. From
/// the outside the hotkey looked like it did nothing at all.
/// </summary>
public partial class FlashWindow : Window
{
    /// <summary>Bright for this long, then gone. Long enough to see, short enough not to be in the way.</summary>
    private static readonly TimeSpan Lit = TimeSpan.FromMilliseconds(160);

    /// <summary>Matches the fade in the markup, plus a beat for the last frame.</summary>
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(220);

    public FlashWindow() => InitializeComponent();

    /// <summary>
    /// Draws the frame around one monitor and takes itself away. Nothing waits
    /// for it: the point is that the answer is instant, while the clipboard and
    /// the folder are still being written.
    /// </summary>
    public static void On(CaptureRect monitor)
    {
        var window = new FlashWindow();

        window.Position = new PixelPoint(monitor.X, monitor.Y);
        window.Fit(monitor);

        window.Show();

        // Again after Show: a window moved onto a monitor with a different
        // scale only learns the new scale once it is there.
        window.Position = new PixelPoint(monitor.X, monitor.Y);
        window.Fit(monitor);

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        WindowSwitcher.Hide(handle);

        // Clicks go through it. It covers a whole monitor for a fifth of a
        // second, and a screenshot is often taken with the mouse already
        // moving towards something.
        WindowClicks.PassThrough(handle);

        // Bright from the first frame, and only the way out is animated. Fading
        // it in as well looked right on paper and was invisible in practice:
        // the fade is longer than the whole flash, so the frame spent its life
        // climbing to a seventh of its brightness and was then told to go back.
        DispatcherTimer.RunOnce(() => window.Frame.Opacity = 0, Lit);
        DispatcherTimer.RunOnce(window.Close, Lit + Fade);
    }

    /// <summary>
    /// One pixel short of the monitor, for the same reason OverlayWindow.Resize
    /// is: a window covering a monitor exactly is a game as far as Windows is
    /// concerned, and the shell reacts to it. Reported live 26.08.2026 as the
    /// taskbar going black for the length of the flash, which is the same trap
    /// the overlay was pulled out of and this window never was.
    ///
    /// In logical units, because that is what Width and Height are. Setting raw
    /// pixels here worked only at 100%; anywhere above it the frame was drawn
    /// larger than the monitor it was supposed to outline.
    /// </summary>
    private void Fit(CaptureRect monitor)
    {
        var scale = RenderScaling;

        Width = monitor.Width / scale;
        Height = (monitor.Height - 1) / scale;
    }
}
