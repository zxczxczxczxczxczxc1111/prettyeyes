using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

public sealed class RectangleTool : ITool
{
    private readonly ToolStyle _style;

    /// <summary>Without a style the tool draws what every version drew before.</summary>
    public RectangleTool(ToolStyle? style = null) => _style = style ?? ToolStyle.Default;

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
        var bounds = CaptureRect.FromPoints(_x, _y, x, y);
        return bounds.IsEmpty
            ? null
            : new RectangleAnnotation(bounds, _style.Color, _style.StrokeWidth);
    }
}
