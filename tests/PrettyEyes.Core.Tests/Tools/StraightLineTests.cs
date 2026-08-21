using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

/// <summary>
/// A segment dragged along one axis has a bounding box of zero height (or zero
/// width), and "the box is empty" used to mean "there is nothing to draw". For
/// a rectangle that is true. For a line it means the tidiest line the user can
/// draw is the one that never appears.
/// </summary>
public class StraightLineTests
{
    [Fact]
    public void A_horizontal_line_is_drawn()
    {
        var tool = new LineTool();
        tool.Begin(10, 50);

        Assert.NotNull(tool.End(200, 50));
    }

    [Fact]
    public void A_vertical_line_is_drawn()
    {
        var tool = new LineTool();
        tool.Begin(10, 50);

        Assert.NotNull(tool.End(10, 300));
    }

    [Fact]
    public void A_horizontal_arrow_is_drawn()
    {
        var tool = new ArrowTool();
        tool.Begin(10, 50);

        Assert.NotNull(tool.End(200, 50));
    }

    [Fact]
    public void The_preview_of_a_horizontal_line_is_drawn_too()
    {
        // The preview is what the user watches while dragging: if only End were
        // fixed, the line would stay invisible until the button came up.
        var tool = new LineTool();
        tool.Begin(10, 50);

        Assert.NotNull(tool.Preview(200, 50));
    }

    [Fact]
    public void A_click_without_a_drag_still_draws_nothing()
    {
        var tool = new LineTool();
        tool.Begin(10, 50);

        Assert.Null(tool.End(10, 50));
    }

    [Fact]
    public void A_rectangle_with_no_height_still_draws_nothing()
    {
        // Not the same case: a rectangle of zero height is a line the user did
        // not ask for, and it stays refused.
        var tool = new RectangleTool();
        tool.Begin(10, 50);

        Assert.Null(tool.End(200, 50));
    }
}
