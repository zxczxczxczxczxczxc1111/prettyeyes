using PrettyEyes.Core.Text;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Text;

public class TextEditorTests
{
    private static SKFont Font => new(SKTypeface.Default, 16);

    [Fact]
    public void A_new_editor_starts_empty_with_the_caret_at_the_front()
    {
        var editor = new TextEditor();

        Assert.Equal(string.Empty, editor.Text);
        Assert.Equal(0, editor.Caret);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void Editing_an_existing_label_starts_with_the_caret_at_the_end()
    {
        var editor = new TextEditor("раз");

        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void Typed_characters_land_where_the_caret_is()
    {
        var editor = new TextEditor("рз");
        editor.MoveTo(1, extend: false);

        editor.Insert("а");

        Assert.Equal("раз", editor.Text);
        Assert.Equal(2, editor.Caret);
    }

    [Fact]
    public void Typing_over_a_selection_replaces_it()
    {
        var editor = new TextEditor("раз два");
        editor.MoveTo(0, extend: false);
        editor.MoveTo(3, extend: true);

        editor.Insert("сто");

        Assert.Equal("сто два", editor.Text);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void Pasted_windows_line_endings_become_plain_ones()
    {
        var editor = new TextEditor();

        editor.Insert("раз\r\nдва");

        Assert.Equal("раз\nдва", editor.Text);
    }

    [Fact]
    public void Control_characters_are_dropped_instead_of_drawn_as_boxes()
    {
        var editor = new TextEditor();

        editor.Insert("раз\tдва");

        Assert.Equal("раздва", editor.Text);
    }

    [Fact]
    public void The_text_stops_growing_at_two_thousand_characters()
    {
        var editor = new TextEditor();

        editor.Insert(new string('a', 2500));

        Assert.Equal(TextEditor.MaxLength, editor.Text.Length);
        Assert.Equal(TextEditor.MaxLength, editor.Caret);
    }

    [Fact]
    public void Backspace_eats_the_character_before_the_caret()
    {
        var editor = new TextEditor("раз");

        editor.Backspace();

        Assert.Equal("ра", editor.Text);
        Assert.Equal(2, editor.Caret);
    }

    [Fact]
    public void Backspace_at_the_very_front_does_nothing_at_all()
    {
        var editor = new TextEditor("раз");
        editor.MoveTo(0, extend: false);

        editor.Backspace();

        Assert.Equal("раз", editor.Text);
    }

    [Fact]
    public void Backspace_with_a_selection_eats_the_selection()
    {
        var editor = new TextEditor("раз два");
        editor.MoveTo(3, extend: false);
        editor.MoveTo(7, extend: true);

        editor.Backspace();

        Assert.Equal("раз", editor.Text);
    }

    [Fact]
    public void Delete_eats_the_character_after_the_caret()
    {
        var editor = new TextEditor("раз");
        editor.MoveTo(0, extend: false);

        editor.Delete();

        Assert.Equal("аз", editor.Text);
        Assert.Equal(0, editor.Caret);
    }

    [Fact]
    public void Select_all_covers_everything_and_leaves_the_caret_at_the_end()
    {
        var editor = new TextEditor("раз два");

        editor.SelectAll();

        Assert.True(editor.HasSelection);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(7, editor.SelectionLength);
    }

    [Fact]
    public void Moving_without_extending_collapses_the_selection()
    {
        var editor = new TextEditor("раз");
        editor.SelectAll();

        editor.MoveTo(1, extend: false);

        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void The_caret_cannot_be_put_outside_the_text()
    {
        var editor = new TextEditor("раз");

        editor.MoveTo(99, extend: false);

        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void Undo_takes_back_a_whole_typed_word_not_one_letter()
    {
        var editor = new TextEditor();
        editor.Insert("р");
        editor.Insert("а");
        editor.Insert("з");

        Assert.True(editor.Undo());
        Assert.Equal(string.Empty, editor.Text);
    }

    [Fact]
    public void Undo_stops_at_the_word_boundary()
    {
        var editor = new TextEditor();

        foreach (var glyph in "раз два")
        {
            editor.Insert(glyph.ToString());
        }

        editor.Undo();

        // The space ends the burst, so one undo leaves the first word alone.
        Assert.Equal("раз ", editor.Text);
    }

    [Fact]
    public void Undo_of_a_deletion_brings_the_characters_back()
    {
        var editor = new TextEditor("раз");

        editor.Backspace();

        Assert.True(editor.Undo());
        Assert.Equal("раз", editor.Text);
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void Undo_with_nothing_behind_it_answers_false()
    {
        Assert.False(new TextEditor("раз").Undo());
    }

    [Fact]
    public void Undo_does_not_reach_past_the_text_it_started_with()
    {
        var editor = new TextEditor("раз");
        editor.Insert("два");

        editor.Undo();

        Assert.False(editor.Undo());
        Assert.Equal("раз", editor.Text);
    }

    [Fact]
    public void A_glyph_the_font_cannot_draw_becomes_one_replacement_sign()
    {
        using var font = Font;

        // Emoji have their own tool. Letting a colour glyph through here means
        // a black box on the screenshot, so it is replaced on the way in.
        var cleaned = TextEditor.Sanitize("a\U0001F600b", font);

        Assert.Equal(3, cleaned.Length);
        Assert.Equal('a', cleaned[0]);
        Assert.Equal('�', cleaned[1]);
        Assert.Equal('b', cleaned[2]);
    }

    [Fact]
    public void Ordinary_letters_survive_the_cleaning_untouched()
    {
        using var font = Font;

        Assert.Equal("раз два", TextEditor.Sanitize("раз два", font));
    }
}
