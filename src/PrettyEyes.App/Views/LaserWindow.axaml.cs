using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Laser;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Views;

/// <summary>
/// A pane of glass over one monitor with the laser trail on it.
///
/// Built on the same three decisions as FlashWindow - no decorations, always on
/// top, never activated - and one that took measuring to arrive at: while the
/// mode is on, this pane owns the mouse.
///
/// The first attempt let clicks through, the way the flash frame does. Two
/// things came out of trying it. Pass-through needs WS_EX_LAYERED - measured,
/// WS_EX_TRANSPARENT alone does not pass a click to another process, and
/// answering the hit test with HTTRANSPARENT does not either - and a layered
/// window at 2560x1440 redrawing continuously runs at ten frames a second
/// against forty-eight without the layer. Pass-through also cannot coexist with
/// the gesture: if the button draws, the same press cannot also land on the
/// application underneath.
///
/// So the mode is a mode. It is turned on to point with and off to go back to
/// using what is being demonstrated, which is how every other pointer that
/// lives over a live screen works, and the cursor says which of the two is
/// happening.
/// </summary>
public partial class LaserWindow : Window
{
    /// <summary>Whether the last repaint put anything on this monitor.</summary>
    private bool _drew;

    public LaserWindow()
    {
        InitializeComponent();

        // The one thing on screen that says the mode is on.
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>The button went down: a stroke starts here.</summary>
    public event EventHandler? StrokeStarted;

    /// <summary>Where the pointer is, in physical pixels of the virtual desktop.</summary>
    public event EventHandler<(double X, double Y)>? Moved;

    public static LaserWindow Open(MonitorInfo monitor, LaserTrail trail)
    {
        var window = new LaserWindow();

        window.Layer.Trail = trail;
        window.Layer.Monitor = monitor.Bounds;

        window.Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);
        window.Width = monitor.Bounds.Width / monitor.Scale;
        window.Height = monitor.Bounds.Height / monitor.Scale;

        window.Show();

        // Again after Show: a window moved onto a monitor with a different
        // scale only learns the new scale once it is there.
        window.Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);

        WindowSwitcher.Hide(window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

        return window;
    }

    /// <summary>
    /// Repaint, because the trail moved on. Called sixty times a second, and
    /// most of the time on a second monitor there is nothing on this pane to
    /// repaint - so it does not, once it has cleared whatever it drew last.
    /// </summary>
    public void Refresh()
    {
        var wanted = Layer.Wanted();

        if (!wanted && !_drew)
        {
            return;
        }

        _drew = wanted;
        Layer.InvalidateVisual();
    }

    /// <summary>How many frames the pane has drawn since it opened.</summary>
    public long Repaints => Layer.Repaints;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            base.OnPointerPressed(e);
            return;
        }

        StrokeStarted?.Invoke(this, EventArgs.Empty);
        Report(e);

        // Held, so a stroke that runs off this monitor keeps arriving here
        // instead of stopping at the edge.
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Report(e);
            e.Handled = true;
            return;
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        base.OnPointerReleased(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Layer.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// Window coordinates are in the units the window is laid out in; the trail
    /// is in physical pixels of the whole desktop, because two panes on two
    /// monitors at two different scales write into the same one.
    /// </summary>
    private void Report(PointerEventArgs e)
    {
        var at = e.GetPosition(this);
        var scale = RenderScaling;

        Moved?.Invoke(this, (Layer.Monitor.X + (at.X * scale), Layer.Monitor.Y + (at.Y * scale)));
    }
}
