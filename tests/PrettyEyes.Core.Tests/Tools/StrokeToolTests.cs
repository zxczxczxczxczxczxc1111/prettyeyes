using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class StrokeToolTests
{
    [Fact]
    public void Bounds_cover_the_whole_gesture()
    {
        var tool = new StrokeTool();
        tool.Begin(20, 20);
        tool.Preview(60, 20);

        var annotation = tool.End(60, 50);

        var stroke = Assert.IsType<StrokeAnnotation>(annotation);
        Assert.True(stroke.Bounds.X < 20);
        Assert.True(stroke.Bounds.Y < 20);
        Assert.True(stroke.Bounds.Right > 60);
        Assert.True(stroke.Bounds.Bottom > 50);
    }

    [Fact]
    public void A_tap_still_leaves_a_dot()
    {
        var tool = new StrokeTool();
        tool.Begin(30, 30);

        // Unlike every other tool: a pencil touched to paper makes a mark, and
        // dropping it would look like the tool had failed.
        Assert.NotNull(tool.End(30, 30));
    }

    [Fact]
    public void Points_closer_than_a_step_are_dropped()
    {
        var tap = new StrokeTool();
        tap.Begin(0, 0);
        var dot = tap.End(0, 0)!;

        // A pixel to the side is the hand shaking, not a change of direction:
        // the mark comes out the same as if the pointer had never moved.
        var shaky = new StrokeTool();
        shaky.Begin(0, 0);
        var barely = shaky.End(1, 0)!;

        var moved = new StrokeTool();
        moved.Begin(0, 0);
        var line = moved.End(6, 0)!;

        Assert.Equal(dot.Bounds, barely.Bounds);
        Assert.True(line.Bounds.Width > barely.Bounds.Width);
    }

    [Fact]
    public void The_highlighter_is_wider_than_the_pencil_it_shares_a_style_with()
    {
        var style = new ToolStyle(Palette.Yellow, StrokeSize.Medium);

        var pencil = new StrokeTool(style);
        pencil.Begin(10, 10);
        var thin = pencil.End(40, 10)!;

        var marker = new StrokeTool(style, highlighter: true);
        marker.Begin(10, 10);
        var thick = marker.End(40, 10)!;

        Assert.True(thick.Bounds.Height > thin.Bounds.Height);
    }

    [Fact]
    public void The_highlighter_starts_out_yellow_and_everything_else_carmine()
    {
        var styles = new ToolStyles();

        Assert.Equal(Palette.Yellow, styles.For(ToolKind.Marker).Color);
        Assert.Equal(Palette.Carmine, styles.For(ToolKind.Pencil).Color);
    }

    [Fact]
    public void Both_new_tools_are_offered_in_the_toolbar()
    {
        Assert.Contains(ToolKind.Pencil, ToolVisibility.All);
        Assert.Contains(ToolKind.Marker, ToolVisibility.All);

        // A tool added later shows up without anyone having chosen it.
        Assert.True(new ToolVisibility().IsShown(ToolKind.Marker));
    }
}
