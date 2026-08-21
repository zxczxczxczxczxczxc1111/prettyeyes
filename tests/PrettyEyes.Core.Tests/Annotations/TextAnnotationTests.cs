using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Annotations;

public class TextAnnotationTests
{
    private static ToolStyle Style => ToolStyle.Default with { FontSize = 16 };

    private static TextAnnotation Label(string text = "abc", int x = 50, int y = 50, int? maxWidth = null) =>
        new(text, x, y, maxWidth, Style);

    private static Document NewDocument()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));

        return new Document(surface.Snapshot(), new CaptureRect(0, 0, 200, 200));
    }

    [Fact]
    public void A_point_inside_the_label_picks_it_up()
    {
        using var document = NewDocument();
        var label = Label();
        document.Add(label);

        Assert.Same(label, document.MovableAt(label.Bounds.X + 1, label.Bounds.Y + 1));
    }

    [Fact]
    public void A_point_outside_the_label_grabs_nothing()
    {
        using var document = NewDocument();
        var label = Label();
        document.Add(label);

        Assert.Null(document.MovableAt(label.Bounds.Right + 5, label.Bounds.Bottom + 5));
    }

    [Fact]
    public void The_one_on_top_wins_when_two_labels_overlap()
    {
        using var document = NewDocument();
        document.Add(Label("under"));
        var over = Label("over");
        document.Add(over);

        // Same placement point, so whatever is returned was chosen by stacking
        // order and not by luck of geometry.
        Assert.Same(over, document.MovableAt(51, 51));
    }

    [Fact]
    public void Moving_carries_the_text_along_with_the_box()
    {
        var moved = (TextAnnotation)Label().MovedBy(10, -5);

        Assert.Equal("abc", moved.Text);
        Assert.Equal(60, moved.Bounds.X);
        Assert.Equal(45, moved.Bounds.Y);
    }

    [Fact]
    public void One_wheel_step_is_worth_two_points_of_type()
    {
        var bigger = (TextAnnotation)Label().ResizedBy(1)!;

        Assert.Equal(18, bigger.Style.FontSize);
    }

    [Fact]
    public void A_bigger_font_needs_a_bigger_box()
    {
        var label = Label();
        var bigger = (TextAnnotation)label.ResizedBy(3)!;

        Assert.True(bigger.Bounds.Width > label.Bounds.Width);
        Assert.True(bigger.Bounds.Height > label.Bounds.Height);
    }

    [Fact]
    public void Growing_happens_around_the_middle_so_the_label_stays_where_it_was()
    {
        var label = Label();
        var bigger = (TextAnnotation)label.ResizedBy(4)!;

        var before = (label.Bounds.X + label.Bounds.Right) / 2;
        var after = (bigger.Bounds.X + bigger.Bounds.Right) / 2;

        Assert.InRange(after, before - 1, before + 1);
    }

    [Fact]
    public void Type_never_goes_below_eight_points()
    {
        var smallest = (TextAnnotation)Label().ResizedBy(-100)!;

        Assert.Equal(8, smallest.Style.FontSize);
    }

    [Fact]
    public void Type_never_goes_above_two_hundred_points()
    {
        var biggest = (TextAnnotation)Label().ResizedBy(500)!;

        Assert.Equal(200, biggest.Style.FontSize);
    }

    [Fact]
    public void A_label_already_at_the_limit_answers_null_instead_of_a_copy()
    {
        var biggest = (TextAnnotation)Label().ResizedBy(500)!;

        Assert.Null(biggest.ResizedBy(1));
    }

    [Fact]
    public void A_width_limit_makes_the_label_taller_and_narrower()
    {
        var free = Label("раз два три четыре пять");
        var boxed = Label("раз два три четыре пять", maxWidth: 60);

        Assert.True(boxed.Bounds.Width < free.Bounds.Width);
        Assert.True(boxed.Bounds.Height > free.Bounds.Height);
    }

    [Fact]
    public void An_empty_label_has_no_box_and_draws_nothing_at_all()
    {
        var empty = Label(string.Empty);

        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Black);
        using var source = surface.Snapshot();

        // No exception and no pixels: an empty label exists only while the
        // caret is in it, and it must not paint a lonely plate on the shot.
        empty.Draw(surface.Canvas, source, new CaptureRect(0, 0, 200, 200));

        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        Assert.True(empty.Bounds.IsEmpty);
        Assert.Equal(SKColors.Black, pixels.GetPixelColor(50, 50));
    }

    [Fact]
    public void The_text_lands_inside_its_own_bounds()
    {
        var label = Label("abc", x: 40, y: 40);

        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Black);
        using var source = surface.Snapshot();

        label.Draw(surface.Canvas, source, new CaptureRect(0, 0, 200, 200));

        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        var painted = 0;

        for (var y = label.Bounds.Y; y < label.Bounds.Bottom; y++)
        {
            for (var x = label.Bounds.X; x < label.Bounds.Right; x++)
            {
                if (pixels.GetPixelColor(x, y) != SKColors.Black)
                {
                    painted++;
                }
            }
        }

        Assert.True(painted > 0, "the label painted nothing at all");
        Assert.Equal(SKColors.Black, pixels.GetPixelColor(label.Bounds.Right + 3, label.Bounds.Bottom + 3));
    }

    [Fact]
    public void The_outline_mode_paints_the_glyphs_in_the_chosen_colour_too()
    {
        var label = new TextAnnotation(
            "abc",
            40,
            40,
            null,
            Style with { Color = Palette.Green, TextBackdrop = TextBackdrop.Outline });

        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Black);
        using var source = surface.Snapshot();

        label.Draw(surface.Canvas, source, new CaptureRect(0, 0, 200, 200));

        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        var green = new SKColor(Palette.Green);
        var found = false;

        for (var y = label.Bounds.Y; y < label.Bounds.Bottom && !found; y++)
        {
            for (var x = label.Bounds.X; x < label.Bounds.Right && !found; x++)
            {
                found = pixels.GetPixelColor(x, y) == green;
            }
        }

        Assert.True(found, "no glyph pixel came out in the chosen colour");
    }
}
