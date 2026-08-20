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
    /// frames at 60 Hz, spent by a window that draws nothing at all, against a
    /// visible flash of the previous capture at the start of the next one.
    /// </summary>
    private static readonly TimeSpan BlankDelay = TimeSpan.FromMilliseconds(50);

    private readonly List<OverlayWindow> _windows = [];
    private readonly DispatcherTimer _blank;

    private bool _warm;

    public OverlayWindowPool()
    {
        _blank = new DispatcherTimer { Interval = BlankDelay };
        _blank.Tick += HideBlanked;
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
        // A capture that comes in before the windows were hidden finds them
        // already empty and simply fills them again; what it must not do is
        // leave a hide pending over the overlay it is about to open.
        _blank.Stop();

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
    /// therefore stores the frame that still has the old capture and the old
    /// selection frame on it, and that is what flashes at the start of the next
    /// capture. Cleared while still on screen, the stored frame is the empty
    /// one, and an empty overlay is invisible.
    /// </summary>
    public void Release()
    {
        foreach (var window in _windows)
        {
            window.Reset();
        }

        _blank.Stop();
        _blank.Start();
    }

    private void HideBlanked(object? sender, EventArgs e)
    {
        _blank.Stop();

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
