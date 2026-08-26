using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Tools;
using SkiaSharp;

namespace PrettyEyes.App.Services;

/// <summary>
/// Builds the tool a toolbar button stands for.
///
/// One place rather than two: the overlay and a pinned window arm the same
/// tools, and a switch copied into both is a switch that gets a new case in one
/// of them.
/// </summary>
public static class ToolMaker
{
    /// <param name="limit">
    /// Where a stamped glyph is allowed to land: the selection in the overlay,
    /// the frame in a pinned window.
    /// </param>
    /// <param name="glyph">
    /// The chosen emoji, already rendered. Asked for lazily, because building
    /// one costs work and every other tool ignores it.
    /// </param>
    public static ITool Create(
        ToolKind kind, ToolStyles styles, CaptureRect limit, Func<SKImage?> glyph) => kind switch
    {
        ToolKind.Blur => new BlurTool(),
        ToolKind.Arrow => Arrow(styles.For(kind)),
        ToolKind.Line => new LineTool(styles.For(kind)),
        ToolKind.Rectangle => new RectangleTool(styles.For(kind)),
        ToolKind.Pencil => new StrokeTool(styles.For(kind)),
        ToolKind.Marker => new StrokeTool(styles.For(kind), highlighter: true),
        ToolKind.Emoji => new EmojiTool(
            glyph() ?? throw new InvalidOperationException("Эмодзи не выбран."),
            limit),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tool."),
    };

    /// <summary>
    /// The only tool with a mood. Read here rather than in the toolbar: the
    /// overlay and a pinned window both arm tools through this class, and a
    /// check copied into both is a check one of them forgets.
    /// </summary>
    private static ITool Arrow(ToolStyle style) =>
        style.FreehandArrow ? new FreehandArrowTool(style) : new ArrowTool(style);
}
