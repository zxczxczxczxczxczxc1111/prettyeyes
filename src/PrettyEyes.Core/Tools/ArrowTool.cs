using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// Unlike the others this one keeps the raw start and end points: an arrow
/// cares about direction, and a normalized rectangle would lose it.
/// </summary>
public sealed class ArrowTool : ITool
{
    private readonly ToolStyle _style;

    /// <summary>Without a style the tool draws what every version drew before.</summary>
    public ArrowTool(ToolStyle? style = null) => _style = style ?? ToolStyle.Default;

    private int _x;
    private int _y;

    public void Begin(int x, int y)
    {
        _x = x;
        _y = y;
    }

    public IAnnotation? Preview(int x, int y) => Build(x, y);

    public IAnnotation? End(int x, int y) => Build(x, y);

    private IAnnotation? Build(int x, int y)
    {
        // Not "is the bounding box empty": a segment dragged along one axis has
        // a box of zero height, and refusing that made the straightest line a
        // person can draw the one line that never appeared. Only a gesture that
        // went nowhere at all is nothing.
        return _x == x && _y == y
            ? null
            : new ArrowAnnotation(_x, _y, x, y, _style.Color, _style.StrokeWidth);
    }
}
