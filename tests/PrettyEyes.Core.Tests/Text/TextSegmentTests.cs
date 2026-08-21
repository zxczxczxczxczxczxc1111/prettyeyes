using PrettyEyes.Core.Text;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Text;

public class TextSegmentTests
{
    private static SKFont Font => new(SKTypeface.Default, 16);

    [Fact]
    public void Every_segment_points_back_at_the_place_it_came_from()
    {
        using var font = Font;

        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(2, segments.Count);
        Assert.Equal(0, segments[0].Start);
        Assert.Equal("раз", segments[0].Text);
        Assert.Equal(4, segments[1].Start);
        Assert.Equal("два", segments[1].Text);
    }

    [Fact]
    public void A_windows_line_break_costs_two_characters_not_one()
    {
        using var font = Font;

        // Offsets are into the original string, warts and all: rewriting it to
        // plain newlines first would shift every caret position after the break.
        var segments = TextLayout.Segments("раз\r\nдва", font, null);

        Assert.Equal(5, segments[1].Start);
    }

    [Fact]
    public void A_text_ending_in_a_break_has_an_empty_line_after_it()
    {
        using var font = Font;

        // Where the caret goes the moment Enter is pressed. Without it the last
        // key of a paragraph puts the caret back on the previous line.
        var segments = TextLayout.Segments("раз\n", font, null);

        Assert.Equal(2, segments.Count);
        Assert.Equal(string.Empty, segments[1].Text);
        Assert.Equal(4, segments[1].Start);
    }

    [Fact]
    public void A_wrapped_line_keeps_pointing_at_the_original_text()
    {
        using var font = Font;
        const string text = "раз два три четыре пять";

        var segments = TextLayout.Segments(text, font, maxWidth: 40);

        Assert.All(segments, segment => Assert.Equal(segment.Text, text.Substring(segment.Start, segment.Text.Length)));
    }

    [Fact]
    public void Segments_and_lines_say_the_same_thing()
    {
        using var font = Font;

        var segments = TextLayout.Segments("раз два три четыре пять", font, maxWidth: 40);
        var lines = TextLayout.Wrap("раз два три четыре пять", font, maxWidth: 40);

        Assert.Equal(lines, segments.Select(segment => segment.Text));
    }

    [Fact]
    public void Doubled_spaces_are_kept_because_somebody_typed_them()
    {
        using var font = Font;

        Assert.Equal("раз  два", TextLayout.Segments("раз  два", font, null)[0].Text);
    }

    [Fact]
    public void The_caret_at_the_start_sits_at_the_padding_and_nowhere_else()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз", font, null);

        var caret = TextLayout.CaretAt(segments, 0, font, padding: 4);

        Assert.Equal(4, caret.X);
        Assert.Equal(4, caret.Y);
    }

    [Fact]
    public void The_caret_walks_right_as_the_index_grows()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз", font, null);

        Assert.True(TextLayout.CaretAt(segments, 3, font, 4).X > TextLayout.CaretAt(segments, 1, font, 4).X);
    }

    [Fact]
    public void The_caret_drops_a_line_after_a_break()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        var first = TextLayout.CaretAt(segments, 1, font, 4);
        var second = TextLayout.CaretAt(segments, 5, font, 4);

        Assert.True(second.Y > first.Y);
        Assert.Equal(first.Height, second.Height);
    }

    [Fact]
    public void The_caret_at_the_end_of_a_line_stays_on_that_line()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        // Index 3 is the newline itself: the caret belongs before the break,
        // not at the start of the line the break creates.
        Assert.Equal(TextLayout.CaretAt(segments, 0, font, 4).Y, TextLayout.CaretAt(segments, 3, font, 4).Y);
    }

    [Fact]
    public void An_empty_text_still_has_somewhere_to_put_the_caret()
    {
        using var font = Font;

        var caret = TextLayout.CaretAt(TextLayout.Segments(string.Empty, font, null), 0, font, padding: 4);

        Assert.Equal(4, caret.X);
        Assert.True(caret.Height > 0);
    }

    [Fact]
    public void A_click_lands_on_the_character_it_was_aimed_at()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз", font, null);

        var caret = TextLayout.CaretAt(segments, 2, font, padding: 4);

        Assert.Equal(2, TextLayout.IndexAt(segments, caret.X, caret.Y + 1, font, padding: 4));
    }

    [Fact]
    public void A_click_past_the_end_of_a_line_goes_to_the_end_of_that_line()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(3, TextLayout.IndexAt(segments, 9999, 5, font, padding: 4));
    }

    [Fact]
    public void A_click_below_the_last_line_goes_to_the_very_end()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(7, TextLayout.IndexAt(segments, 9999, 9999, font, padding: 4));
    }

    [Fact]
    public void A_click_above_the_first_line_goes_to_the_very_start()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(0, TextLayout.IndexAt(segments, -50, -50, font, padding: 4));
    }
}
