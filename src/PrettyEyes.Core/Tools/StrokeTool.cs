using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// Freehand drawing: pencil when opaque, highlighter when not.
///
/// The only tool that keeps the whole gesture rather than two points, so it is
/// also the only one where the preview has to stay cheap: points closer than a
/// step apart are dropped on the way in.
/// </summary>
public sealed class StrokeTool : ITool
{
    /// <summary>
    /// Points nearer than this to the last one are noise from the hand and the
    /// mouse. Measured in physical pixels, so a 4K screen keeps its detail.
    /// </summary>
    private const int Step = 2;

    private readonly ToolStyle _style;
    private readonly bool _highlighter;
    private readonly List<(int X, int Y)> _points = [];

    public StrokeTool(ToolStyle? style = null, bool highlighter = false)
    {
        _style = style ?? ToolStyle.Default;
        _highlighter = highlighter;
    }

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

        return new StrokeAnnotation(_points, _style.Color, _style.StrokeWidth, _highlighter);
    }
}
