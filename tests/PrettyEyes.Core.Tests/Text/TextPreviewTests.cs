using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Text;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PrettyEyes.Core.Tests.Text;

public class TextPreviewTests
{
    private static ToolStyle Style => ToolStyle.Default with { Color = Palette.Green, FontSize = 16 };

    private static TextAnnotation Label(string text) => new(text, 40, 40, null, Style);

    private static (SKSurface Surface, SKImage Source) Canvas()
    {
        var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Black);

        return (surface, surface.Snapshot());
    }

    private static int Count(SKSurface surface, SKColor colour, CaptureRect within)
    {
        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();

        var found = 0;

        for (var y = Math.Max(0, within.Y); y < Math.Min(200, within.Bottom); y++)
        {
            for (var x = Math.Max(0, within.X); x < Math.Min(200, within.Right); x++)
            {
                if (pixels.GetPixelColor(x, y) == colour)
                {
                    found++;
                }
            }
        }

        return found;
    }

    [Fact]
    public void The_caret_is_drawn_while_it_is_on_and_gone_while_it_is_off()
    {
        var label = Label("abc");
        var editor = new TextEditor("abc");
        editor.MoveTo(0, extend: false);

        var (on, source) = Canvas();
        using (source)
        using (on)
        {
            new TextPreview(label, editor, caretOn: true).Draw(on.Canvas, source, CaptureRect.Empty);

            var (off, _) = Canvas();

            using (off)
            {
                new TextPreview(label, editor, caretOn: false).Draw(off.Canvas, source, CaptureRect.Empty);

                // The caret is drawn in the colour of the text: it is the
                // text, one character early.
                var lit = Count(on, new SKColor(Palette.Green), label.Bounds);
                var dark = Count(off, new SKColor(Palette.Green), label.Bounds);

                Assert.True(lit > dark, "the caret painted nothing while it was on");
            }
        }
    }

    [Fact]
    public void An_empty_label_still_shows_a_caret_to_type_at()
    {
        var label = Label(string.Empty);
        var editor = new TextEditor();

        var (surface, source) = Canvas();

        using (source)
        using (surface)
        {
            var preview = new TextPreview(label, editor, caretOn: true);
            preview.Draw(surface.Canvas, source, CaptureRect.Empty);

            // The label has no box of its own yet, so the preview has to own one
            // or the caret has nowhere to be.
            Assert.False(preview.Bounds.IsEmpty);
            Assert.True(Count(surface, new SKColor(Palette.Green), preview.Bounds) > 0);
        }
    }

    [Fact]
    public void A_selection_is_painted_behind_the_text_and_not_over_it()
    {
        var label = Label("abc");
        var editor = new TextEditor("abc");
        editor.SelectAll();

        var (surface, source) = Canvas();

        using (source)
        using (surface)
        {
            new TextPreview(label, editor, caretOn: false).Draw(surface.Canvas, source, CaptureRect.Empty);

            var (plain, _) = Canvas();

            using (plain)
            {
                new TextPreview(label, new TextEditor("abc"), caretOn: false)
                    .Draw(plain.Canvas, source, CaptureRect.Empty);

                Assert.NotEqual(Pixels(plain), Pixels(surface));
            }

            // The glyphs are still their own colour: a highlight drawn on top
            // would hide the very text being selected.
            Assert.True(Count(surface, new SKColor(Palette.Green), label.Bounds) > 0);
        }
    }

    [Fact]
    public void With_nothing_selected_and_no_caret_the_preview_is_the_label()
    {
        var label = Label("abc");

        var (preview, source) = Canvas();
        var (plain, _) = Canvas();

        using (source)
        using (preview)
        using (plain)
        {
            new TextPreview(label, new TextEditor("abc"), caretOn: false)
                .Draw(preview.Canvas, source, CaptureRect.Empty);

            label.Draw(plain.Canvas, source, CaptureRect.Empty);

            // Nothing extra creeps in between keystrokes: what is on screen
            // while typing is what stays there after the label is committed.
            Assert.Equal(Pixels(plain), Pixels(preview));
        }
    }

    private static byte[] Pixels(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    [Fact]
    public void The_preview_covers_the_label_it_previews()
    {
        var label = Label("abc");

        var preview = new TextPreview(label, new TextEditor("abc"), caretOn: true);

        Assert.Equal(label.Bounds.X, preview.Bounds.X);
        Assert.True(preview.Bounds.Width >= label.Bounds.Width);
    }
}
