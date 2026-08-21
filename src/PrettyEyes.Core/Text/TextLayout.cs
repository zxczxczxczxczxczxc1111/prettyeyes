using System.Collections.Concurrent;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Tools;
using SkiaSharp;

namespace PrettyEyes.Core.Text;

/// <summary>
/// Turns a blob of typed text into the lines that will actually be drawn, and
/// tells you how much room they take.
///
/// Split out of the annotation on purpose: the overlay needs the same numbers
/// while the caret is still blinking, long before anything is committed to the
/// document. Two implementations of "where does this line end" would disagree
/// on the first sentence somebody types.
/// </summary>
public static class TextLayout
{
    /// <summary>
    /// Typefaces are cached because building one walks the font directory, and
    /// a label is measured on every rendered frame while it is being dragged.
    /// SKTypeface is immutable and safe to share; SKFont is not, so callers get
    /// a fresh one of those every time.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SKTypeface> Typefaces = new();

    /// <summary>
    /// The font a style asks for. An unknown family name falls back to the
    /// system default rather than throwing: the name may have come from a
    /// settings file written on a machine that had the font.
    /// </summary>
    public static SKFont FontFor(ToolStyle style) =>
        new(style.FontFamily is null
                ? SKTypeface.Default
                : Typefaces.GetOrAdd(style.FontFamily, name => SKTypeface.FromFamilyName(name) ?? SKTypeface.Default),
            style.FontSize);

    /// <summary>
    /// Hard line breaks are always honoured. A width limit additionally breaks
    /// between words, and inside a word when the word alone does not fit.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, SKFont font, int? maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = new List<string>();

        // Trailing \r would otherwise be measured as a glyph and quietly widen
        // every line pasted from a Windows editor.
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (maxWidth is not > 0)
            {
                lines.Add(paragraph);
                continue;
            }

            WrapParagraph(paragraph, font, maxWidth.Value, lines);
        }

        return lines;
    }

    /// <summary>
    /// Box of the given lines, with the origin at the placement point: the
    /// caller owns the position, this only owns the size.
    /// </summary>
    public static CaptureRect Measure(IReadOnlyList<string> lines, SKFont font, int padding)
    {
        if (lines.Count == 0)
        {
            return CaptureRect.Empty;
        }

        var width = 0f;

        foreach (var line in lines)
        {
            width = Math.Max(width, font.MeasureText(line));
        }

        // Spacing, not the glyph box: two lines of the same text must be twice
        // as tall as one, whether or not anybody typed a descender.
        var height = font.Spacing * lines.Count;

        return new CaptureRect(
            0,
            0,
            (int)Math.Ceiling(width) + (padding * 2),
            (int)Math.Ceiling(height) + (padding * 2));
    }

    private static void WrapParagraph(string paragraph, SKFont font, int maxWidth, List<string> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        var current = string.Empty;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;

            if (font.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            current = font.MeasureText(word) <= maxWidth
                ? word
                : BreakWord(word, font, maxWidth, lines);
        }

        // A paragraph made of nothing but spaces still occupies a line, because
        // the caret is sitting on it.
        lines.Add(current);
    }

    /// <summary>
    /// Chops a word that does not fit on a line of its own, emitting every full
    /// chunk and answering with the leftover that starts the next line.
    /// </summary>
    private static string BreakWord(string word, SKFont font, int maxWidth, List<string> lines)
    {
        var chunk = string.Empty;

        foreach (var glyph in word)
        {
            var candidate = chunk + glyph;

            // One character always goes through even when it is wider than the
            // limit: the alternative is an empty chunk and an endless loop.
            if (chunk.Length > 0 && font.MeasureText(candidate) > maxWidth)
            {
                lines.Add(chunk);
                chunk = glyph.ToString();
                continue;
            }

            chunk = candidate;
        }

        return chunk;
    }
}
