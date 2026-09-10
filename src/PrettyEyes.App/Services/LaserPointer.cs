using System.Diagnostics;
using Avalonia.Threading;
using PrettyEyes.Core.Laser;

namespace PrettyEyes.App.Services;

/// <summary>
/// The clock behind the laser trail: what ages it, and when whoever draws it
/// needs to hear about that.
///
/// One object for both modes, and neither of them reads the cursor: both have a
/// window that already has the pointer events and hands them over. The timer
/// exists only for the fade, so it runs while something is on screen and stops
/// the moment nothing is - the next press starts it again.
/// </summary>
public sealed class LaserPointer : IDisposable
{
    /// <summary>
    /// How long a point stays on screen. Long enough to underline a sentence
    /// at reading speed and still have the beginning of it lit when the hand
    /// reaches the end, which is the gesture people actually make - a circle
    /// round something takes less.
    ///
    /// Excalidraw uses a second. That is a second for a mouse in a browser
    /// window; over a whole desktop the same gesture covers more ground and
    /// takes longer.
    /// </summary>
    private static readonly TimeSpan Life = TimeSpan.FromMilliseconds(1600);

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer;

    /// <summary>Whether the last frame put anything on screen.</summary>
    private bool _lit;

    public LaserPointer()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = Frame };
        _timer.Tick += (_, _) => Beat();
    }

    public LaserTrail Trail { get; } = new(Life);

    /// <summary>A frame's worth of change. Whoever draws the trail repaints here.</summary>
    public event EventHandler? Changed;

    /// <summary>The button went down somewhere that already had the event.</summary>
    public void Begin() => Trail.Begin();

    /// <summary>
    /// A position from somewhere that already had the event.
    ///
    /// Recorded, not drawn. A mouse reports faster than a screen refreshes -
    /// measured at 120 positions a second against a 60 hertz monitor - and
    /// repainting on each one is half the work thrown away. The timer decides
    /// when a frame happens; this only decides what is in it.
    /// </summary>
    public void Trace(double x, double y)
    {
        Trail.Add(x, y, _clock.Elapsed);
        _lit = true;

        // Keeps running after the pointer stops, or the last centimetre of the
        // trail would hang there until something else happened to repaint.
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        Trail.Clear();

        if (!_lit)
        {
            return;
        }

        _lit = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _timer.Stop();

    private void Beat()
    {
        Trail.Advance(_clock.Elapsed);

        var lit = Trail.Count > 0;

        if (!lit && !_lit)
        {
            // Nothing on screen and nothing left to take off it. Positions
            // arrive as events, so there is nothing for the timer to watch
            // for: the next press starts it again.
            _timer.Stop();

            return;
        }

        _lit = lit;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
