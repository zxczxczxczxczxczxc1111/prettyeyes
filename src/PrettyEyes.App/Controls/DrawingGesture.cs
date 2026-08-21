using Avalonia;
using Avalonia.Input;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Controls;

/// <summary>
/// Drawing with a tool, and carrying a stamped object with Ctrl. The part of a
/// pointer gesture that means the same thing in the overlay and in a pinned
/// window, so it lives in one place rather than in two that drift apart.
///
/// It is <b>called</b>, never subscribed. The overlay has work to do before
/// drawing gets a say - closing an open card, the double click that takes the
/// whole monitor, the selection grips - and a handler of its own would race all
/// of it for the same press.
///
/// What stayed behind on purpose: selection and its grips, the magnifier, the
/// style card, the mode machine, and text. A pinned window has none of those.
/// </summary>
public sealed class DrawingGesture
{
    private readonly Func<Point, (int X, int Y)> _toVirtual;
    private readonly Action<IAnnotation?> _preview;
    private readonly Func<CaptureRect> _limit;
    private readonly Func<Document?> _document;
    private readonly Func<ITool?> _toolFactory;

    /// <summary>The object being carried, and where it was picked up.</summary>
    private IMovable? _moving;
    private int _fromX;
    private int _fromY;

    private ITool? _tool;

    /// <param name="toVirtual">Window point to virtual-desktop pixels.</param>
    /// <param name="preview">Where the half-finished shape is shown.</param>
    /// <param name="limit">
    /// The rectangle a drawn point is clamped into. A function rather than a
    /// value: in the overlay it is the selection, which changes under the user.
    /// </param>
    /// <param name="document">What is under the pointer, for the carry.</param>
    /// <param name="toolFactory">The armed tool, or null when there is none.</param>
    public DrawingGesture(
        Func<Point, (int X, int Y)> toVirtual,
        Action<IAnnotation?> preview,
        Func<CaptureRect> limit,
        Func<Document?> document,
        Func<ITool?> toolFactory)
    {
        _toVirtual = toVirtual;
        _preview = preview;
        _limit = limit;
        _document = document;
        _toolFactory = toolFactory;
    }

    /// <summary>A tool gesture produced a shape.</summary>
    public event EventHandler<IAnnotation>? Drawn;

    /// <summary>Something was carried somewhere else and let go.</summary>
    public event EventHandler<(IMovable Annotation, int Dx, int Dy)>? Moved;

    /// <summary>The wheel over an object asked for a size step.</summary>
    public event EventHandler<(IMovable Annotation, int Step)>? Resized;

    /// <summary>Something is in mid-air right now.</summary>
    public bool Carrying => _moving is not null;

    /// <summary>
    /// Takes the press if it belongs to drawing or to a carry. False means the
    /// host still owns this press and should go on with its own handling.
    /// </summary>
    public bool Press(Point at, KeyModifiers modifiers, bool armed)
    {
        var (x, y) = _toVirtual(at);

        // Ctrl picks a stamped object up instead of drawing on top of it. Held
        // rather than plain, because a plain drag over one has to keep meaning
        // what it means with every tool: draw a new one.
        if (modifiers.HasFlag(KeyModifiers.Control) && _document()?.MovableAt(x, y) is { } carried)
        {
            _moving = carried;
            _fromX = x;
            _fromY = y;

            var document = _document();

            if (document is not null)
            {
                document.Detached = carried;
            }

            _preview(carried);

            return true;
        }

        if (!armed)
        {
            return false;
        }

        var (toolX, toolY) = _limit().ClampPoint(x, y);

        _tool = _toolFactory();
        _tool?.Begin(toolX, toolY);

        return true;
    }

    /// <summary>True when this move belonged to a gesture in progress.</summary>
    public bool Move(Point at)
    {
        if (_moving is null && _tool is null)
        {
            return false;
        }

        var (x, y) = _toVirtual(at);

        if (_moving is { } carried)
        {
            _preview(carried.MovedBy(x - _fromX, y - _fromY));

            return true;
        }

        var (toolX, toolY) = _limit().ClampPoint(x, y);
        _preview(_tool?.Preview(toolX, toolY));

        return true;
    }

    /// <summary>True when this release ended a gesture of ours.</summary>
    public bool Release(Point at)
    {
        if (_moving is null && _tool is null)
        {
            return false;
        }

        var (x, y) = _toVirtual(at);

        if (_moving is { } carried)
        {
            Drop();
            Moved?.Invoke(this, (carried, x - _fromX, y - _fromY));

            return true;
        }

        var (toolX, toolY) = _limit().ClampPoint(x, y);
        var annotation = _tool?.End(toolX, toolY);

        _tool = null;
        _preview(null);

        if (annotation is not null)
        {
            Drawn?.Invoke(this, annotation);
        }

        return true;
    }

    /// <summary>
    /// Ctrl and the wheel over a stamped object makes it bigger or smaller.
    /// The same modifier that picks one up, so there is one thing to remember
    /// rather than two.
    /// </summary>
    public bool Wheel(Point at, KeyModifiers modifiers, double delta)
    {
        if (!modifiers.HasFlag(KeyModifiers.Control) || _moving is not null)
        {
            return false;
        }

        var (x, y) = _toVirtual(at);

        if (_document()?.MovableAt(x, y) is not { } target)
        {
            return false;
        }

        Resized?.Invoke(this, (target, delta > 0 ? 1 : -1));

        return true;
    }

    /// <summary>
    /// Esc while something is in mid-air. It goes back where it was, and the
    /// host is told so: putting it back is a change like any other, and the
    /// one place that can redraw every monitor has to hear about it.
    /// </summary>
    public bool CancelCarry()
    {
        if (_moving is not { } dropped)
        {
            return false;
        }

        Drop();

        // Nowhere is a legal destination.
        Moved?.Invoke(this, (dropped, 0, 0));

        return true;
    }

    /// <summary>
    /// Whether Ctrl here would pick something up rather than draw. The host
    /// paints the cursor itself: the crosshair chooses its ink from the pixels
    /// under it, and that is the host's canvas, not this class's business.
    /// </summary>
    public bool WouldCarry(int x, int y, KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) && _document()?.MovableAt(x, y) is not null;

    /// <summary>
    /// Forgets any gesture in progress, silently. For a window that is pooled
    /// and reused: a tool left half-begun here would finish itself into the
    /// next capture.
    /// </summary>
    public void Reset()
    {
        _moving = null;
        _tool = null;
    }

    /// <summary>Puts the carried object back into the picture.</summary>
    private void Drop()
    {
        _moving = null;

        // Back into the document first: showing the preview redraws, and a
        // frame drawn between the two would show the object nowhere at all.
        var document = _document();

        if (document is not null)
        {
            document.Detached = null;
        }

        _preview(null);
    }
}
