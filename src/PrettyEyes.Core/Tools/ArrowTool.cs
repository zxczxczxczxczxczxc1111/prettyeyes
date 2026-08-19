using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// Unlike the others this one keeps the raw start and end points: an arrow
/// cares about direction, and a normalized rectangle would lose it.
/// </summary>
public sealed class ArrowTool : ITool
{
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
        var line = CaptureRect.FromPoints(_x, _y, x, y);
        return line.IsEmpty
            ? null
            : new ArrowAnnotation(_x, _y, x, y, AnnotationColors.Shape, AnnotationColors.StrokeWidth);
    }
}
