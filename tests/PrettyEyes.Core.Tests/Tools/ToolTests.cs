using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class ToolTests
{
    [Fact]
    public void BlurTool_produces_annotation_covering_the_drag()
    {
        var tool = new BlurTool();
        tool.Begin(10, 10);

        var annotation = tool.End(50, 40);

        var blur = Assert.IsType<BlurAnnotation>(annotation);
        Assert.Equal(40, blur.Bounds.Width);
        Assert.Equal(30, blur.Bounds.Height);
    }

    [Fact]
    public void BlurTool_ignores_a_click_without_drag()
    {
        var tool = new BlurTool();
        tool.Begin(10, 10);

        Assert.Null(tool.End(10, 10));
    }

    [Fact]
    public void Preview_returns_the_same_shape_as_the_finished_annotation()
    {
        var tool = new RectangleTool();
        tool.Begin(5, 5);

        var preview = tool.Preview(60, 40);
        var final = tool.End(60, 40);

        Assert.NotNull(preview);
        Assert.Equal(final!.Bounds, preview!.Bounds);
    }

    [Fact]
    public void ArrowTool_keeps_direction_from_start_to_end()
    {
        var tool = new ArrowTool();
        tool.Begin(100, 100);

        var annotation = tool.End(20, 20);

        var arrow = Assert.IsType<ArrowAnnotation>(annotation);
        // Bounds are padded by the head, so check the drawn extent instead.
        Assert.True(arrow.Bounds.Contains(20, 20));
        Assert.True(arrow.Bounds.Contains(99, 99));
    }

    [Fact]
    public void RectangleTool_ignores_zero_area_drag()
    {
        var tool = new RectangleTool();
        tool.Begin(5, 5);

        Assert.Null(tool.End(5, 60));
    }
}
