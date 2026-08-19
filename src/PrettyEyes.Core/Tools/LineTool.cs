using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// Same gesture as the arrow, without the head. Keeps the raw points: a line
/// drawn right to left is still drawn right to left.
/// </summary>
public sealed class LineTool : ITool
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
            : new LineAnnotation(_x, _y, x, y, AnnotationColors.Shape, AnnotationColors.StrokeWidth);
    }
}
