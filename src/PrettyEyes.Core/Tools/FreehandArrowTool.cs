using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// The arrow drawn the way a pencil is: the whole gesture is kept, and the head
/// is stamped on wherever the hand stopped.
///
/// The one place it parts with the pencil is the click that went nowhere. A
/// pencil touched to paper leaves a dot on purpose; an arrow with no direction
/// is a blot, so a gesture that never moved gives nothing.
/// </summary>
public sealed class FreehandArrowTool : ITool
{
    /// <summary>
    /// The same threshold the pencil uses, and for the same reason: anything
    /// nearer than this to the last point is the hand and the mouse, not a
    /// change of direction. Physical pixels, so a 4K screen keeps its detail.
    /// </summary>
    private const int Step = 2;

    private readonly ToolStyle _style;
    private readonly List<(int X, int Y)> _points = [];

    public FreehandArrowTool(ToolStyle? style = null) => _style = style ?? ToolStyle.Default;

    public void Begin(int x, int y)
    {
        _points.Clear();
        _points.Add((x, y));
    }

    public IAnnotation? Preview(int x, int y) => Build(x, y);

    public IAnnotation? End(int x, int y) => Build(x, y);

    private IAnnotation? Build(int x, int y)
    {
        if (_points.Count == 0)
        {
            return null;
        }

        var (lastX, lastY) = _points[^1];

        if (Math.Abs(x - lastX) >= Step || Math.Abs(y - lastY) >= Step)
        {
            _points.Add((x, y));
        }

        // Counted, not compared against the release point. A twitch of one
        // pixel is below the step, so nothing is recorded, yet the release
        // coordinate differs from the start - and comparing those two let a
        // single-point arrow through as a headless dot, which is exactly the
        // blot this tool refuses to draw.
        return _points.Count < 2
            ? null
            : new FreehandArrowAnnotation(_points, _style.Color, _style.StrokeWidth);
    }
}
