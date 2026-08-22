namespace PrettyEyes.Core.Capture;

/// <summary>
/// Says when something has been left alone long enough to be let go of.
///
/// A tray application spends its whole life idle and takes a screenshot once
/// an hour, yet the capture engine holds a Direct3D device, a desktop-sized
/// buffer and a texture per monitor the entire time. Measured on the author's
/// machine: 285 MB sitting still, of which about 95 is exactly that.
///
/// Time is passed in rather than read from the clock, because a rule about
/// elapsed time that asks the system what time it is cannot be tested.
/// </summary>
public sealed class IdleWatch
{
    private readonly TimeSpan _after;
    private readonly object _gate = new();

    private DateTime _touched;
    private bool _released;

    public IdleWatch(TimeSpan after)
    {
        _after = after;

        // Nothing has been built yet, so there is nothing to let go of.
        _released = true;
    }

    /// <summary>Something was just used. The clock starts again.</summary>
    public void Touch(DateTime now)
    {
        lock (_gate)
        {
            _touched = now;
            _released = false;
        }
    }

    /// <summary>
    /// True exactly once per idle spell: the caller is expected to act on it,
    /// and asking again before the next use must not release twice.
    /// </summary>
    public bool Due(DateTime now)
    {
        lock (_gate)
        {
            if (_released || now - _touched < _after)
            {
                return false;
            }

            _released = true;

            return true;
        }
    }
}
