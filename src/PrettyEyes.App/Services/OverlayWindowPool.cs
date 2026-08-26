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

    /// <summary>
    /// How long the pool waits for the emptying frame before hiding the window
    /// regardless. Generous, because the normal answer arrives inside one
    /// frame and this is only the floor under a compositor having a bad
    /// moment; bounded, because until it fires a window nobody can see is
    /// still on top of that monitor.
    /// </summary>
    private static readonly TimeSpan HideCap = TimeSpan.FromMilliseconds(250);

    private readonly List<OverlayWindow> _windows = [];
    private readonly DispatcherTimer _blank;
    private readonly DispatcherTimer _hide;

    private Action? _retire;

    private bool _warm;

    /// <summary>
    /// Which capture the windows belong to. Bumped whenever they change hands,
    /// so an answer about a frame that mattered two captures ago cannot hide a
    /// window that is showing the current one.
    /// </summary>
    private int _generation;

    public OverlayWindowPool()
    {
        _blank = new DispatcherTimer { Interval = BlankDelay };
        _blank.Tick += HideBlanked;

        _hide = new DispatcherTimer { Interval = HideCap };
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
        // Every capture is a new generation: answers owed to the previous one
        // must not act on windows this one is about to fill.
        _generation++;

        // A capture that arrives while the previous overlay is still fading out
        // takes the windows as they are and fills them again; what it must not
        // do is leave a hide pending over the overlay it is about to open.
        _blank.Stop();
        _hide.Stop();
        Clear();
        Retire();


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

    /// <summary>
    /// Emptied now, hidden once the emptying is on screen.
    ///
    /// A hidden window keeps whatever was last composited into it, and nothing
    /// can repaint it while it is hidden. Clearing and hiding in the same breath
    /// therefore stores the frame that still has the previous capture on it,
    /// dimmed and with its selection frame, and the next capture shows that for
    /// as long as it takes to draw its own - which is the flash. Cleared while
    /// still on screen, what gets stored is the empty frame, and the window is
    /// transparent, so the empty frame is nothing at all.
    ///
    /// This used to wait a fixed 50 ms for that emptying frame and hide
    /// regardless. Measured 26.08.2026 over eight closes: one of them hid a
    /// window that had not repainted at all, so the flash this whole dance
    /// exists to prevent was still happening, roughly one reopen in eight and
    /// possibly more - the counter watches repaints being requested, not frames
    /// reaching the screen. The wait is now asked of the compositor and the
    /// timer is only a cap; see HideBlanked and HideEmptied.
    /// </summary>
    public void Release(TimeSpan after, Action retire)
    {
        _retire = retire;
        _blank.Stop();
        _blank.Interval = after + BlankDelay;
        _blank.Start();
    }

    private void HideBlanked(object? sender, EventArgs e)
    {
        _blank.Stop();

        Clear();

        // TEMPORARY diagnostics: see HideEmptied.
        Log.Default.Info($"гашение, после Clear: пусто {string.Join(", ", _windows.Select(w => w.Blanks))}"
            + $" / с картинкой {string.Join(", ", _windows.Select(w => w.Repaints))}");

        // Hidden once the emptying frame is actually on screen, not a fixed
        // beat later. A hidden window keeps whatever the compositor last put
        // on it, so hiding one that has not repainted yet stores the previous
        // capture, drawings and all; see Release. Measured over eight closes,
        // one of them hid a window that had never repainted.
        var generation = _generation;
        var painted = _windows.Select(window => window.Painted()).OfType<Task>().ToArray();

        // The cap runs regardless: a compositor that never answers must not
        // leave a full-screen topmost window on the desktop eating clicks.
        _hide.Stop();
        _hide.Start();

        if (painted.Length == 0)
        {
            HideNow(generation);
            return;
        }

        // Rendered invokes its continuations synchronously and can land on the
        // render thread; the windows belong to the UI one.
        Task.WhenAll(painted).ContinueWith(
            _ => Dispatcher.UIThread.Post(() => HideNow(generation)),
            TaskScheduler.Default);
    }

    /// <summary>Empties the windows. What they were showing stays alive.</summary>
    private void Clear()
    {
        foreach (var window in _windows)
        {
            window.Reset();
        }
    }

    /// <summary>
    /// Frees the capture the windows were showing.
    ///
    /// Deliberately not done when they are emptied, and not when they are
    /// hidden either. Drawing happens on its own thread, and a frame handed to
    /// it a moment ago can still be executing after the window has been told to
    /// let go; freeing the image out from under it takes the whole process down
    /// without so much as an exception to log. Waiting until the next capture
    /// costs one frame of memory while idle and removes the race entirely.
    /// </summary>
    private void Retire()
    {
        var retire = _retire;
        _retire = null;
        retire?.Invoke();
    }

    /// <summary>
    /// The cap fired: the emptying frame did not arrive in the time allowed.
    /// Hidden anyway, because a full-screen topmost window left on the desktop
    /// swallows every click on its monitor, and one flash is the lesser harm.
    /// Logged rather than silent: this is exactly the case the old fixed beat
    /// used to hit blind, eight closes out of eight.
    /// </summary>
    private void HideEmptied(object? sender, EventArgs e)
    {
        Log.Default.Info("гашение: кадр не пришёл за отведённое время, прячу как есть");

        HideNow(_generation);
    }

    /// <summary>
    /// Hides the windows, unless a new capture has claimed them meanwhile.
    ///
    /// Reached from two places that can both be late: the cap timer, and the
    /// compositor answering that the emptying frame is on screen. Between the
    /// request and either answer a hotkey can arrive, and hiding then would
    /// pull the overlay out from under the person using it. Hence the
    /// generation.
    /// </summary>
    private void HideNow(int generation)
    {
        if (generation != _generation)
        {
            return;
        }

        _hide.Stop();

        // TEMPORARY diagnostics for the flash of the previous capture on
        // reopen. Compared against the line written by HideBlanked: an
        // unchanged blank count means the emptying repaint never ran before
        // the window was hidden, and the frame frozen into it is still the
        // previous capture.
        Log.Default.Info($"гашение, перед скрытием: пусто {string.Join(", ", _windows.Select(w => w.Blanks))}"
            + $" / с картинкой {string.Join(", ", _windows.Select(w => w.Repaints))}");

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
        _generation++;

        // A monitor change in the middle of a fade: the windows are about to be
        // destroyed, so whatever is waiting on them has to be told now or the
        // capture it is holding is never released.
        _blank.Stop();
        _hide.Stop();
        Clear();
        Retire();

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
