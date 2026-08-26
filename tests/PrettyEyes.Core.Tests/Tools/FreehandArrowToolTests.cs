using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class FreehandArrowToolTests
{
    [Fact]
    public void A_drag_gives_a_freehand_arrow()
    {
        var tool = new FreehandArrowTool();
        tool.Begin(10, 10);
        tool.Preview(60, 10);

        Assert.IsType<FreehandArrowAnnotation>(tool.End(120, 10));
    }

    [Fact]
    public void A_click_that_went_nowhere_is_nothing()
    {
        // Unlike the pencil, which leaves a dot. An arrow that points nowhere
        // is not a mark anybody meant to make.
        var tool = new FreehandArrowTool();
        tool.Begin(30, 30);

        Assert.Null(tool.End(30, 30));
    }

    [Fact]
    public void A_twitch_shorter_than_a_step_is_nothing_either()
    {
        // One pixel is below the step, so no second point is recorded and the
        // gesture is still a single dot. Comparing the release against the
        // start instead of counting points let this through as a headless blot:
        // 0 != 1, so it looked like movement.
        var tool = new FreehandArrowTool();
        tool.Begin(0, 0);

        Assert.Null(tool.End(1, 0));
    }

    [Fact]
    public void Points_closer_than_a_step_are_dropped()
    {
        var tool = new FreehandArrowTool();
        tool.Begin(0, 0);
        tool.Preview(1, 0);

        // One pixel is the hand shaking. The gesture still has to end up as an
        // arrow, just without that point in it.
        Assert.IsType<FreehandArrowAnnotation>(tool.End(80, 0));
    }

    [Fact]
    public void The_style_decides_the_width()
    {
        var thin = new FreehandArrowTool(new ToolStyle(Palette.Blue, StrokeSize.Small));
        thin.Begin(0, 0);
        var narrow = thin.End(100, 0)!;

        var thick = new FreehandArrowTool(new ToolStyle(Palette.Blue, StrokeSize.Large));
        thick.Begin(0, 0);
        var wide = thick.End(100, 0)!;

        // The head grows with the stroke, so the padded bounds grow with it too.
        Assert.True(wide.Bounds.Width > narrow.Bounds.Width);
    }
}
