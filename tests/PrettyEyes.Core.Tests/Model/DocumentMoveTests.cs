using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Model;

public class DocumentMoveTests
{
    private static SKImage NewImage(int size = 8)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        surface.Canvas.Clear(SKColors.Yellow);

        return surface.Snapshot();
    }

    private static Document NewDocument() => new(NewImage(64), new CaptureRect(0, 0, 64, 64));

    private static EmojiAnnotation Glyph(SKImage image, int x, int y, int size = 40) =>
        new(new CaptureRect(x, y, size, size), image);

    [Fact]
    public void Nothing_is_found_where_nothing_was_drawn()
    {
        using var glyph = NewImage();
        using var document = NewDocument();

        document.Add(Glyph(glyph, 100, 100));

        Assert.Null(document.MovableAt(50, 50));
    }

    [Fact]
    public void A_glyph_is_found_under_a_point_inside_it()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);

        Assert.Same(emoji, document.MovableAt(120, 120));
    }

    [Fact]
    public void The_topmost_of_two_stacked_glyphs_wins()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var under = Glyph(glyph, 100, 100);
        var over = Glyph(glyph, 110, 110);

        document.Add(under);
        document.Add(over);

        // The point is inside both; the one drawn last is the one on top, and
        // the one anybody would say they are pointing at.
        Assert.Same(over, document.MovableAt(120, 120));
    }

    [Fact]
    public void Moving_shifts_the_bounds_and_leaves_the_size_alone()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);

        Assert.True(document.Move(emoji, 30, -20));

        var moved = Assert.Single(document.Annotations);

        Assert.Equal(new CaptureRect(130, 80, 40, 40), moved.Bounds);
    }

    [Fact]
    public void A_move_keeps_the_glyph_where_it_was_in_the_stack()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var first = Glyph(glyph, 0, 0);
        var second = Glyph(glyph, 200, 200);

        document.Add(first);
        document.Add(second);
        document.Move(first, 5, 5);

        // Still first, still underneath: moving is not a reason to come forward.
        Assert.Equal(new CaptureRect(5, 5, 40, 40), document.Annotations[0].Bounds);
        Assert.Same(second, document.Annotations[1]);
    }

    [Fact]
    public void Moving_nowhere_is_not_a_change()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);

        Assert.False(document.Move(emoji, 0, 0));
    }

    [Fact]
    public void Moving_something_that_was_never_added_changes_nothing()
    {
        using var glyph = NewImage();
        using var document = NewDocument();

        Assert.False(document.Move(Glyph(glyph, 0, 0), 10, 10));
        Assert.Empty(document.Annotations);
    }

    [Fact]
    public void Undo_takes_back_a_move_in_one_step()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);
        document.Move(emoji, 40, 40);

        Assert.True(document.Undo());

        var restored = Assert.Single(document.Annotations);

        Assert.Equal(new CaptureRect(100, 100, 40, 40), restored.Bounds);
    }

    [Fact]
    public void Undo_after_a_move_still_reaches_the_empty_picture()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);
        document.Move(emoji, 10, 10);

        Assert.True(document.Undo());
        Assert.True(document.Undo());
        Assert.Empty(document.Annotations);
        Assert.False(document.Undo());
    }

    [Fact]
    public void A_detached_annotation_is_not_in_the_snapshot()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);
        document.Detached = emoji;

        Assert.Empty(document.SnapshotAnnotations());

        document.Detached = null;

        Assert.Single(document.SnapshotAnnotations());
    }

    [Fact]
    public void Clearing_forgets_the_history_too()
    {
        using var glyph = NewImage();
        using var document = NewDocument();

        document.Add(Glyph(glyph, 0, 0));
        document.Clear();

        // Otherwise undo would bring back shapes drawn for a region that no
        // longer exists.
        Assert.False(document.Undo());
    }

    [Fact]
    public void A_step_of_the_wheel_grows_the_glyph_around_its_centre()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);

        Assert.True(document.Resize(emoji, 1));

        var grown = Assert.Single(document.Annotations);

        Assert.Equal(40 + EmojiAnnotation.SizeStep, grown.Bounds.Width);
        Assert.Equal(100 - (EmojiAnnotation.SizeStep / 2), grown.Bounds.X);
        Assert.Equal(100 - (EmojiAnnotation.SizeStep / 2), grown.Bounds.Y);
    }

    [Fact]
    public void The_glyph_does_not_shrink_past_the_smallest_readable_size()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 0, 0, EmojiTool.MinSize);

        document.Add(emoji);

        Assert.False(document.Resize(emoji, -1));
        Assert.Equal(EmojiTool.MinSize, document.Annotations[0].Bounds.Width);
    }

    [Fact]
    public void The_glyph_does_not_grow_past_the_largest()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 0, 0, EmojiTool.MaxSize);

        document.Add(emoji);

        Assert.False(document.Resize(emoji, 1));
    }

    [Fact]
    public void A_run_of_wheel_steps_undoes_in_one()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var emoji = Glyph(glyph, 100, 100);

        document.Add(emoji);

        var current = emoji;

        for (var step = 0; step < 4; step++)
        {
            Assert.True(document.Resize(current, 1));
            current = (EmojiAnnotation)document.Annotations[0];
        }

        Assert.True(document.Undo());

        // Back to the size it was stamped at, not four notches of undo.
        Assert.Equal(40, document.Annotations[0].Bounds.Width);
    }

    [Fact]
    public void Drawing_between_two_runs_of_steps_splits_them()
    {
        using var glyph = NewImage();
        using var document = NewDocument();
        var first = Glyph(glyph, 0, 0);

        document.Add(first);
        document.Resize(first, 1);

        var second = Glyph(glyph, 200, 200);
        document.Add(second);
        document.Resize(second, 1);

        Assert.True(document.Undo());

        // The second glyph is back to its stamped size, the first keeps its step.
        Assert.Equal(40, document.Annotations[1].Bounds.Width);
        Assert.Equal(40 + EmojiAnnotation.SizeStep, document.Annotations[0].Bounds.Width);
    }
}
