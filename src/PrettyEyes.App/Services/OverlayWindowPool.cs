using Avalonia.Threading;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;

namespace PrettyEyes.App.Services;

/// <summary>
/// Keeps one overlay window per monitor alive between captures.
///
/// Measured on two monitors: building the windows lazily costs 105 ms on the
/// first hotkey and 42 ms on every one after that, which is as much as the
/// screen capture itself. The windows carry no state of their own once
/// released, so keeping them costs a handle each and buys back the wait.
///
/// Lives on the UI thread: windows belong to the thread that creates them.
/// </summary>
public sealed class OverlayWindowPool
{
    /// <summary>
    /// How long an emptied window stays on screen before it is hidden. Three
    /// frames at 60 Hz, spent by a window that draws nothing at all.
    /// </summary>
    private static readonly TimeSpan BlankDelay = TimeSpan.FromMilliseconds(50);

    private readonly List<OverlayWindow> _windows = [];
    private readonly DispatcherTimer _blank;
    private readonly DispatcherTimer _hide;

    private Action? _cleared;

    private bool _warm;

    public OverlayWindowPool()
    {
        _blank = new DispatcherTimer { Interval = BlankDelay };
        _blank.Tick += HideBlanked;

        _hide = new DispatcherTimer { Interval = BlankDelay };
        _hide.Tick += HideEmptied;
    }

    /// <summary>
    /// Builds the windows and pays the one-off cost while nobody is waiting.
    /// Called at start-up and again whenever the monitor layout changes.
    /// </summary>
    public void Warm(int monitors)
    {
        using var scope = Log.Default.Scope("pool.warm");

        Resize(monitors);

        foreach (var window in _windows)
        {
            window.WarmUp();
        }

        _warm = true;
    }

    /// <summary>The windows for this capture, one per monitor, hidden.</summary>
    public IReadOnlyList<OverlayWindow> Take(DesktopLayout layout)
    {
        // A capture that arrives while the previous overlay is still fading out
        // takes the windows as they are and fills them again; what it must not
        // do is leave a hide pending over the overlay it is about to open.
        _blank.Stop();
        _hide.Stop();
        Clear();


        if (!_warm)
        {
            Warm(layout.Monitors.Count);
        }
        else
        {
            Resize(layout.Monitors.Count);
        }

        return _windows;
    }

    /// <summary>Hides the windows and forgets the capture they were showing.</summary>
    /// <summary>
    /// Emptied now, hidden a moment later.
    ///
    /// A hidden window keeps whatever was last composited into it, and nothing
    /// can repaint it while it is hidden. Clearing and hiding in the same breath
    /// therefore stores the frame that still has the previous capture on it,
    /// dimmed and with its selection frame, and the next capture shows that for
    /// as long as it takes to draw its own - which is the flash. Cleared while
    /// still on screen, what gets stored is the empty frame, and the window is
    /// transparent, so the empty frame is nothing at all.
    /// </summary>
    public void Release(TimeSpan after, Action cleared)
    {
        _cleared = cleared;
        _blank.Stop();
        _blank.Interval = after + BlankDelay;
        _blank.Start();
    }

    private void HideBlanked(object? sender, EventArgs e)
    {
        _blank.Stop();

        Clear();

        // Hidden one beat after being emptied, so the frame stored in a hidden
        // window is the empty one rather than the last capture; see Release.
        _hide.Stop();
        _hide.Start();
    }

    /// <summary>
    /// Lets go of the capture, and tells whoever was holding it that they can
    /// too. Until this runs the windows are still drawing that image, so the
    /// frame it came from has to stay alive.
    /// </summary>
    private void Clear()
    {
        foreach (var window in _windows)
        {
            window.Reset();
        }

        var cleared = _cleared;
        _cleared = null;
        cleared?.Invoke();
    }

    private void HideEmptied(object? sender, EventArgs e)
    {
        _hide.Stop();

        foreach (var window in _windows)
        {
            window.Hide();
        }
    }

    /// <summary>
    /// A monitor came or went. The windows are sized per monitor at placement
    /// time, so only the count matters here.
    /// </summary>
    public void Rebuild(int monitors)
    {
        // A monitor change in the middle of a fade: the windows are about to be
        // destroyed, so whatever is waiting on them has to be told now or the
        // capture it is holding is never released.
        _blank.Stop();
        _hide.Stop();
        Clear();

        foreach (var window in _windows)
        {
            window.Close();
        }

        _windows.Clear();
        _warm = false;

        Warm(monitors);
    }

    private void Resize(int monitors)
    {
        while (_windows.Count < monitors)
        {
            var window = new OverlayWindow();
            _windows.Add(window);

            // A monitor plugged in later gets the same treatment as the rest.
            if (_warm)
            {
                window.WarmUp();
            }
        }

        while (_windows.Count > monitors)
        {
            var last = _windows[^1];
            _windows.RemoveAt(_windows.Count - 1);
            last.Close();
        }
    }
}
