namespace PrettyEyes.Core.Laser;

/// <summary>One place the pointer has been, and how far through its life it is.</summary>
/// <param name="Age">Nothing at the pointer, one at the moment it disappears.</param>
public readonly record struct LaserPoint(float X, float Y, float Age);

/// <summary>
/// The tail behind the laser pointer: a ring of recent positions that expire
/// from the far end.
///
/// Fixed capacity and no allocation once built. This runs sixty times a second
/// over the whole desktop while somebody is presenting, and the project has a
/// measured allocation figure for drawing that is not going to be spent on a
/// pointer.
///
/// Times come in from the caller rather than a clock inside: two windows on two
/// monitors share one trail, and a trail that read its own clock would age
/// differently in each of them.
/// </summary>
public sealed class LaserTrail
{
    private readonly TimeSpan _life;
    private readonly float[] _x;
    private readonly float[] _y;
    private readonly TimeSpan[] _stamp;

    private int _start;
    private int _count;
    private TimeSpan _now;

    private float _lastX;
    private float _lastY;
    private bool _hasLast;

    /// <param name="capacity">
    /// Enough for a fast mouse. A pointer reporting at a thousand hertz fills
    /// a second's worth of tail well before this, and the far end of the tail
    /// is the part nobody looks at.
    /// </param>
    public LaserTrail(TimeSpan life, int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(life, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);

        _life = life;
        _x = new float[capacity];
        _y = new float[capacity];
        _stamp = new TimeSpan[capacity];
    }

    public int Count => _count;

    public bool IsEmpty => _count == 0;

    /// <summary>Oldest first: drawing is a walk from the end of the tail up to the pointer.</summary>
    public LaserPoint this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);

            var slot = (_start + index) % _x.Length;
            var age = (_now - _stamp[slot]) / _life;

            return new LaserPoint(_x[slot], _y[slot], (float)Math.Clamp(age, 0, 1));
        }
    }

    /// <summary>
    /// Records where the pointer is, and drops whatever has run out on the way
    /// in - so the count is honest without a separate call on every frame.
    /// </summary>
    public void Add(double x, double y, TimeSpan now)
    {
        Advance(now);

        var nextX = (float)x;
        var nextY = (float)y;

        // A pointer that has stopped reports the same place every frame.
        // Compared against the last position offered rather than the last one
        // still alive: otherwise the tail dies, the next identical report is
        // taken as new, and standing still becomes a dot that never fades.
        if (_hasLast && nextX == _lastX && nextY == _lastY)
        {
            return;
        }

        _lastX = nextX;
        _lastY = nextY;
        _hasLast = true;

        if (_count == _x.Length)
        {
            // Full: the far end of the tail goes, never the pointer.
            _start = (_start + 1) % _x.Length;
            _count--;
        }

        var slot = (_start + _count) % _x.Length;

        _x[slot] = nextX;
        _y[slot] = nextY;
        _stamp[slot] = now;
        _count++;
    }

    /// <summary>
    /// Hands the live points over as a flat block, oldest first, and says how
    /// many. What crosses to the render thread: the trail itself keeps being
    /// written while a frame is being drawn from it.
    /// </summary>
    public int CopyTo(Span<LaserPoint> destination)
    {
        if (destination.Length < _count)
        {
            throw new ArgumentException(
                $"the trail holds {_count} points and the copy has room for {destination.Length}.",
                nameof(destination));
        }

        for (var index = 0; index < _count; index++)
        {
            destination[index] = this[index];
        }

        return _count;
    }

    /// <summary>
    /// Moves the clock on without adding anything. What the last frames of a
    /// fade are made of, once the pointer has stopped.
    /// </summary>
    public void Advance(TimeSpan now)
    {
        _now = now;

        while (_count > 0 && now - _stamp[_start] > _life)
        {
            _start = (_start + 1) % _x.Length;
            _count--;
        }
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
        _hasLast = false;
    }
}
