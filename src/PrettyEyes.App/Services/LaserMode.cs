using System.Diagnostics;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Services;

/// <summary>
/// The laser pointer over the live desktop: a click-through pane per monitor
/// and the trail drawn on all of them from one set of points.
///
/// Separate from the mode inside the capture overlay, which draws the same
/// trail with the same painter but has a frozen screen underneath it and a
/// toolbar to be switched off from.
/// </summary>
public sealed class LaserMode : IDisposable
{
    private readonly List<LaserWindow> _windows = [];

    /// <summary>The monitors the panes were built for, in the same order.</summary>
    private IReadOnlyList<MonitorInfo> _screens = [];
    private readonly LaserPointer _pointer = new();

    private AppServices? _services;

    /// <summary>True between StepAside and StepBack, and only then.</summary>
    private bool _stepped;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    /// <summary>Below this a pointer stops looking like a pointer.</summary>
    private const double Slow = 40;

    private long _measuredFrom = Stopwatch.GetTimestamp();
    private long _asked;
    private long _drawn;

    public LaserMode() => _pointer.Changed += (_, _) => Repaint();

    /// <summary>Turned on or off, whoever did it. The tray menu reads this.</summary>
    public event EventHandler? Switched;

    public bool IsOn => _windows.Count > 0;

    /// <summary>
    /// Late-bound the same way the pins are: services are built after the
    /// windows that use them.
    /// </summary>
    public void Use(AppServices services) => _services = services;

    public void Toggle()
    {
        if (IsOn)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void Start()
    {
        if (_services is null || IsOn)
        {
            return;
        }

        _screens = _services.Monitors.Enumerate().Monitors;

        foreach (var monitor in _screens)
        {
            var window = LaserWindow.Open(monitor, _pointer.Trail);

            // Held, not hovered: this is the excalidraw gesture, and a trail
            // that followed the cursor around unasked would be on the screen
            // the whole time somebody was using what is being demonstrated.
            window.StrokeStarted += (_, _) => _pointer.Begin();
            window.Moved += (_, at) => _pointer.Trace(at.X, at.Y);

            _windows.Add(window);
        }

        // The fade runs on a sixteen-millisecond tick, which on the default
        // system timer fires at forty-four hertz. Given back in Stop.
        FineTimers.Hold();

        Log.Default.Info($"указка включена, окон {_windows.Count}");
        Switched?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsOn)
        {
            return;
        }

        _pointer.Stop();
        FineTimers.Release();

        foreach (var window in _windows)
        {
            window.Close();
        }

        _windows.Clear();
        _screens = [];
        _stepped = false;

        Log.Default.Info("указка выключена");
        Switched?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Monitors were rearranged. The panes are sized and placed for a desktop
    /// that no longer exists, so they are built again rather than moved.
    /// </summary>
    public void Rehome()
    {
        if (!IsOn)
        {
            return;
        }

        Stop();
        Start();
    }

    /// <summary>
    /// Takes the panes off the screen and waits until that is true of the
    /// pixels as well. Returns whether there was anything to take off, so the
    /// caller knows whether to put it back.
    ///
    /// A screenshot with the pointer's own trail baked into it is a screenshot
    /// nobody asked for, and unlike the pins this cannot be solved with display
    /// affinity: a pane invisible to capture is also invisible on the shared
    /// screen, which is the one place it has to be seen.
    /// </summary>
    public bool StepAside()
    {
        if (!IsOn || _stepped)
        {
            return false;
        }

        foreach (var window in _windows)
        {
            window.Hide();
        }

        Composition.Settle();
        _stepped = true;

        return true;
    }

    public void StepBack()
    {
        if (!_stepped)
        {
            return;
        }

        _stepped = false;

        for (var index = 0; index < _windows.Count; index++)
        {
            _windows[index].Show();
            _windows[index].Settle(_screens[index]);
        }
    }

    public void Dispose()
    {
        _pointer.Dispose();
        Stop();
    }

    private void Repaint()
    {
        foreach (var window in _windows)
        {
            window.Refresh();
        }

        Measure();
    }

    /// <summary>
    /// Says so in the log when the pointer is not keeping up, and says which
    /// half is behind: how often a repaint was asked for, and how often one
    /// happened. A pane that is asked sixty times and draws five is a
    /// compositor problem; one that is asked five times is a timer problem.
    /// Silent while it is fine, so a demonstration does not fill the log.
    /// </summary>
    private void Measure()
    {
        _asked++;

        var elapsed = Stopwatch.GetElapsedTime(_measuredFrom);

        if (elapsed < Window)
        {
            return;
        }

        var drawn = _windows.Count > 0 ? _windows[0].Repaints : 0;
        var asked = _asked / elapsed.TotalSeconds;
        var rate = (drawn - _drawn) / elapsed.TotalSeconds;

        // Nobody asked for a frame, so nobody was let down: the second after
        // the button comes up is all fade and then nothing, and a line saying
        // "asked 0, drew 0" is a complaint about silence.
        if (asked >= Slow && rate < Slow)
        {
            // The ASCII tail is not decoration: the load script runs in a console
            // that mangles Cyrillic, and it matches on "fps".
            Log.Default.Info($"указка: просили {asked:F0} кадров в секунду, нарисовано {rate:F0} (fps)");
        }

        _asked = 0;
        _drawn = drawn;
        _measuredFrom = Stopwatch.GetTimestamp();
    }
}
