using PrettyEyes.Core.Text;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Text;

/// <summary>
/// Home, End and the vertical arrows work on the lines the user sees, which are
/// the wrapped ones. Only the layout knows where those are, so they live next
/// to it rather than in the editor.
/// </summary>
public class TextNavigationTests
{
    private static SKFont Font => new(SKTypeface.Default, 16);

    [Fact]
    public void Home_goes_to_the_start_of_the_line_the_caret_is_on()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(4, TextLayout.LineStart(segments, 6));
    }

    [Fact]
    public void End_goes_to_the_end_of_the_line_the_caret_is_on()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(3, TextLayout.LineEnd(segments, 1));
    }

    [Fact]
    public void Home_on_a_wrapped_line_stops_at_the_wrap_not_at_the_paragraph()
    {
        using var font = Font;
        const string text = "раз два три четыре пять";
        var segments = TextLayout.Segments(text, font, maxWidth: 40);

        // The second visual line has no character of its own to start at, which
        // is exactly why the offsets exist.
        Assert.Equal(segments[1].Start, TextLayout.LineStart(segments, segments[1].Start + 1));
    }

    [Fact]
    public void Up_lands_on_the_line_above_at_roughly_the_same_place()
    {
        using var font = Font;
        var segments = TextLayout.Segments("разум\nдвасе", font, null);

        // Index 8 is the third character of the second line, so the answer is
        // the third character of the first line, give or take a pixel.
        var above = TextLayout.Above(segments, 8, font, padding: 4);

        Assert.InRange(above, 1, 3);
    }

    [Fact]
    public void Up_from_the_first_line_goes_to_the_very_start()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(0, TextLayout.Above(segments, 2, font, padding: 4));
    }

    [Fact]
    public void Down_lands_on_the_line_below()
    {
        using var font = Font;
        var segments = TextLayout.Segments("разум\nдвасе", font, null);

        Assert.InRange(TextLayout.Below(segments, 2, font, padding: 4), 7, 9);
    }

    [Fact]
    public void Down_from_the_last_line_goes_to_the_very_end()
    {
        using var font = Font;
        var segments = TextLayout.Segments("раз\nдва", font, null);

        Assert.Equal(7, TextLayout.Below(segments, 5, font, padding: 4));
    }

    [Fact]
    public void Navigating_an_empty_text_stays_at_zero()
    {
        using var font = Font;
        var segments = TextLayout.Segments(string.Empty, font, null);

        Assert.Equal(0, TextLayout.LineStart(segments, 0));
        Assert.Equal(0, TextLayout.LineEnd(segments, 0));
        Assert.Equal(0, TextLayout.Above(segments, 0, font, 4));
        Assert.Equal(0, TextLayout.Below(segments, 0, font, 4));
    }
}
