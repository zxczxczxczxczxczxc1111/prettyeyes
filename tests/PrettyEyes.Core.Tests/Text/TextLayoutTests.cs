using PrettyEyes.Core.Text;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Text;

public class TextLayoutTests
{
    // A fresh font per call: SKFont is not thread safe and xunit runs classes
    // in parallel, which is a lovely way to get a heisenbug for free.
    private static SKFont Font => new(SKTypeface.Default, 16);

    [Fact]
    public void A_newline_typed_by_hand_survives_into_its_own_line()
    {
        using var font = Font;

        Assert.Equal(2, TextLayout.Wrap("раз\nдва", font, maxWidth: null).Count);
    }

    [Fact]
    public void An_empty_line_in_the_middle_is_kept_because_the_caret_lives_there()
    {
        using var font = Font;

        Assert.Equal(new[] { "раз", string.Empty, "два" }, TextLayout.Wrap("раз\n\nдва", font, null));
    }

    [Fact]
    public void Windows_line_endings_do_not_leave_a_stray_carriage_return()
    {
        using var font = Font;

        Assert.Equal(new[] { "раз", "два" }, TextLayout.Wrap("раз\r\nдва", font, null));
    }

    [Fact]
    public void A_line_wider_than_the_limit_is_broken_between_words()
    {
        using var font = Font;

        var lines = TextLayout.Wrap("раз два три четыре пять", font, maxWidth: 40);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.DoesNotContain("  ", line));
    }

    [Fact]
    public void Without_a_limit_nothing_is_broken_no_matter_how_long()
    {
        using var font = Font;

        Assert.Single(TextLayout.Wrap("раз два три четыре пять", font, maxWidth: null));
    }

    [Fact]
    public void A_single_word_wider_than_the_limit_is_broken_mid_word()
    {
        using var font = Font;

        // No space to break at. Either the word is chopped or the box grows to
        // the width of the desktop, and one of those is not a feature.
        var lines = TextLayout.Wrap("непереносимоедлинноеслово", font, maxWidth: 40);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(font.MeasureText(line) <= 40 || line.Length == 1));
        Assert.Equal("непереносимоедлинноеслово", string.Concat(lines));
    }

    [Fact]
    public void Bounds_are_measured_after_wrapping_and_not_before()
    {
        using var font = Font;

        // Bounds computed before wrapping means Ctrl-drag misses the label the
        // user is looking at, which is a fun bug to explain.
        var lines = TextLayout.Wrap("раз два три четыре пять", font, maxWidth: 40);
        var bounds = TextLayout.Measure(lines, font, padding: 4);

        Assert.True(bounds.Height > font.Size);
    }

    [Fact]
    public void Padding_is_added_on_both_sides_of_the_widest_line()
    {
        using var font = Font;
        var lines = TextLayout.Wrap("раз", font, null);

        var bare = TextLayout.Measure(lines, font, padding: 0);
        var padded = TextLayout.Measure(lines, font, padding: 4);

        Assert.Equal(bare.Width + 8, padded.Width);
        Assert.Equal(bare.Height + 8, padded.Height);
    }

    [Fact]
    public void Bounds_start_at_the_placement_point_itself()
    {
        using var font = Font;

        var bounds = TextLayout.Measure(TextLayout.Wrap("раз", font, null), font, padding: 4);

        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
    }

    [Fact]
    public void Empty_text_gives_empty_bounds_instead_of_a_lonely_padding_box()
    {
        using var font = Font;

        var bounds = TextLayout.Measure(TextLayout.Wrap(string.Empty, font, null), font, padding: 4);

        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
    }

    [Fact]
    public void A_second_line_costs_another_line_of_spacing()
    {
        using var font = Font;

        var one = TextLayout.Measure(TextLayout.Wrap("раз", font, null), font, padding: 0);
        var two = TextLayout.Measure(TextLayout.Wrap("раз\nдва", font, null), font, padding: 0);

        // Not exactly double: the total is rounded up once, not once per line,
        // so a half-pixel spacing does not pile up into a gap by line twenty.
        Assert.InRange(two.Height, (one.Height * 2) - 1, one.Height * 2);
    }
}
