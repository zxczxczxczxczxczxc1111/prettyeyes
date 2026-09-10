namespace PrettyEyes.Core.Laser;

/// <summary>One place the pointer has been, and how far through its life it is.</summary>
/// <param name="Age">Nothing at the pointer, one at the moment it disappears.</param>
/// <param name="Break">
/// True where a new stroke begins. Without it the drawing joins the end of one
/// stroke to the start of the next and paints a line nobody drew.
/// </param>
public readonly record struct LaserPoint(float X, float Y, float Age, bool Break);

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
    /// <summary>
    /// How far a new point is allowed to be from the last kept one, as a
    /// fraction of the distance between them. Excalidraw calls the same number
    /// streamline and runs at 0.4, meaning a point lands six tenths of the way
    /// towards where the mouse says it is.
    ///
    /// This is what turns a reported staircase into something an arm looks like
    /// it drew, and it costs one multiply.
    /// </summary>
    private const float Pull = 0.6f;

    private readonly TimeSpan _life;
    private readonly float[] _x;
    private readonly float[] _y;
    private readonly TimeSpan[] _stamp;
    private readonly bool[] _break;

    private int _start;
    private int _count;
    private TimeSpan _now;

    /// <summary>Where the pointer last said it was, before smoothing.</summary>
    private float _lastX;
    private float _lastY;
    private bool _hasLast;

    /// <summary>Where the last point was actually put, after smoothing.</summary>
    private float _keptX;
    private float _keptY;

    /// <summary>Set by Begin, cleared by the point that opens the stroke.</summary>
    private bool _opening = true;

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
        _break = new bool[capacity];
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

            return new LaserPoint(_x[slot], _y[slot], (float)Math.Clamp(age, 0, 1), _break[slot]);
        }
    }

    /// <summary>
    /// The button went down: what comes next is a new stroke. Whatever is
    /// already in the trail stays and keeps fading where it lies - taken off
    /// mid-fade it would read as a glitch rather than as a new stroke.
    /// </summary>
    public void Begin()
    {
        _opening = true;
        _hasLast = false;
    }

    /// <summary>
    /// Records where the pointer is, and drops whatever has run out on the way
    /// in - so the count is honest without a separate call on every frame.
    /// </summary>
    public void Add(double x, double y, TimeSpan now)
    {
        Advance(now);

        var rawX = (float)x;
        var rawY = (float)y;

        // A pointer being held still reports the same place every frame.
        // Compared against the last position offered rather than the last one
        // still alive: otherwise the tail dies, the next identical report is
        // taken as new, and standing still becomes a dot that never fades.
        if (_hasLast && rawX == _lastX && rawY == _lastY)
        {
            return;
        }

        _lastX = rawX;
        _lastY = rawY;

        var opening = _opening;

        if (opening || !_hasLast)
        {
            // Where the button went down, exactly. Smoothed, the stroke would
            // start somewhere between here and wherever the last one ended.
            _keptX = rawX;
            _keptY = rawY;
        }
        else
        {
            _keptX += (rawX - _keptX) * Pull;
            _keptY += (rawY - _keptY) * Pull;
        }

        _hasLast = true;
        _opening = false;

        if (_count == _x.Length)
        {
            // Full: the far end of the tail goes, never the pointer.
            _start = (_start + 1) % _x.Length;
            _count--;
        }

        var slot = (_start + _count) % _x.Length;

        _x[slot] = _keptX;
        _y[slot] = _keptY;
        _stamp[slot] = now;
        _break[slot] = opening;
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

        // The oldest surviving point now opens whatever is left, or the drawing
        // would run a stroke back to a point that has already expired.
        if (_count > 0)
        {
            _break[_start] = true;
        }
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
        _hasLast = false;
        _opening = true;
    }
}
