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
    private readonly List<OverlayWindow> _windows = [];

    private bool _warm;

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
    public void Release()
    {
        foreach (var window in _windows)
        {
            window.Reset();
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
