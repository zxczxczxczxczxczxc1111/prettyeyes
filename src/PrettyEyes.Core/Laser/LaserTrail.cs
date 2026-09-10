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

    /// <summary>
    /// How far the pointer has to have moved before that counts as somewhere
    /// new, in pixels.
    ///
    /// A mouse reports far faster than it moves - a thousand hertz is a report
    /// every fraction of a pixel - and one point per report means the length of
    /// trail a buffer holds depends on whose mouse it is. Measured in pixels,
    /// it does not: the buffer holds a distance.
    ///
    /// Two, because the beam's edge is antialiased and its narrowest pass is a
    /// pixel and a bit wide, so a corner cut two pixels early is a corner
    /// nobody can see.
    /// </summary>
    private const float Step = 2f;

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
    /// Points, and with a point costing two pixels of movement, half as many
    /// pixels of gesture. The default holds a stroke a screen and a half long,
    /// which is more than the painter draws before the beam tapers out - so
    /// what limits the length is the drawing, where it can be looked at, and
    /// not a buffer size nobody sees.
    /// </param>
    public LaserTrail(TimeSpan life, int capacity = 640)
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

        // A pointer being held still reports the same place every frame, and
        // one being dragged slowly reports a fraction of a pixel at a time.
        // Compared against the last position taken rather than the last one
        // offered: otherwise a slow drag is a series of moves that are each
        // too small and never adds up to anything. That also means the tail
        // dying does not make the next identical report new, so standing still
        // stays a dot that fades rather than one that never does.
        if (_hasLast && Near(rawX, rawY))
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

    /// <summary>
    /// Whether a reported position is close enough to the last one taken to be
    /// the same place. Squared, because a square root to answer a comparison is
    /// a square root on every report a mouse sends.
    /// </summary>
    private bool Near(float x, float y)
    {
        var dx = x - _lastX;
        var dy = y - _lastY;

        return (dx * dx) + (dy * dy) < Step * Step;
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
        _hasLast = false;
        _opening = true;
    }
}
