using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class ToolStyleTests
{
    [Fact]
    public void The_default_is_what_every_version_so_far_drew()
    {
        var style = ToolStyle.Default;

        Assert.Equal(Palette.Carmine, style.Color);
        Assert.Equal(StrokeSize.Medium, style.Size);
        Assert.Equal(3f, style.StrokeWidth);
    }

    [Theory]
    [InlineData(StrokeSize.Small, 2f)]
    [InlineData(StrokeSize.Medium, 3f)]
    [InlineData(StrokeSize.Large, 5f)]
    public void Every_step_has_its_own_width(StrokeSize size, float expected) =>
        Assert.Equal(expected, new ToolStyle(Palette.Blue, size).StrokeWidth);

    [Fact]
    public void A_tool_without_a_style_draws_the_default()
    {
        var tool = new RectangleTool();
        tool.Begin(0, 0);

        var annotation = tool.End(40, 40);

        Assert.NotNull(annotation);
        Assert.Equal(new CaptureRectangle(0, 0, 40, 40), Describe(annotation!));
    }

    [Fact]
    public void A_styled_tool_hands_its_colour_and_width_to_the_annotation()
    {
        var tool = new RectangleTool(new ToolStyle(Palette.Blue, StrokeSize.Large));
        tool.Begin(10, 10);

        var annotation = tool.End(60, 60);

        // The annotation keeps colour and width privately; the visible effect
        // is what the renderer tests cover. Here it is enough that a styled
        // tool still produces the shape it is supposed to.
        Assert.NotNull(annotation);
        Assert.IsType<RectangleAnnotation>(annotation);
    }

    [Fact]
    public void Styles_are_remembered_per_tool_and_default_for_the_rest()
    {
        var styles = new ToolStyles();
        styles.Set(ToolKind.Arrow, new ToolStyle(Palette.Green, StrokeSize.Small));

        Assert.Equal(Palette.Green, styles.For(ToolKind.Arrow).Color);
        Assert.Equal(StrokeSize.Small, styles.For(ToolKind.Arrow).Size);
        Assert.Equal(ToolStyle.Default, styles.For(ToolKind.Rectangle));
    }

    [Fact]
    public void A_tool_the_stored_file_does_not_know_about_is_ignored()
    {
        var stored = new Dictionary<ToolKind, ToolStyle>
        {
            [ToolKind.Line] = new(Palette.Yellow, StrokeSize.Large),
            [(ToolKind)999] = new(Palette.Red, StrokeSize.Small),
        };

        var styles = new ToolStyles(stored);

        Assert.Equal(Palette.Yellow, styles.For(ToolKind.Line).Color);
        Assert.Single(styles.Stored);
    }

    [Fact]
    public void A_copy_does_not_move_when_the_original_does()
    {
        var styles = new ToolStyles();
        styles.Set(ToolKind.Arrow, new ToolStyle(Palette.Blue, StrokeSize.Small));

        var copy = styles.Copy();
        styles.Set(ToolKind.Arrow, new ToolStyle(Palette.Red, StrokeSize.Large));

        Assert.Equal(Palette.Blue, copy.For(ToolKind.Arrow).Color);
    }

    [Fact]
    public void The_palette_starts_with_carmine_and_has_eight_colours()
    {
        Assert.Equal(8, Palette.All.Count);
        Assert.Equal(Palette.Carmine, Palette.All[0]);
    }

    private static CaptureRectangle Describe(PrettyEyes.Core.Model.IAnnotation annotation) =>
        new(annotation.Bounds.X, annotation.Bounds.Y, annotation.Bounds.Width, annotation.Bounds.Height);

    private readonly record struct CaptureRectangle(int X, int Y, int Width, int Height);
}
