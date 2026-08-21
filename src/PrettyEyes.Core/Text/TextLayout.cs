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
///
/// Everything here works in offsets into the original string and never rewrites
/// it. Normalising line endings first would be simpler and would put the caret
/// one character off after every break pasted from a Windows editor.
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
        var segments = Segments(text, font, maxWidth);
        var lines = new List<string>(segments.Count);

        foreach (var segment in segments)
        {
            lines.Add(segment.Text);
        }

        return lines;
    }

    /// <summary>
    /// The same lines, each carrying the offset it starts at. Characters eaten
    /// by a break - the newline itself, the space a wrap happened at - belong to
    /// no segment, which is exactly what makes the offsets add up.
    /// </summary>
    public static IReadOnlyList<TextSegment> Segments(string text, SKFont font, int? maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var segments = new List<TextSegment>();
        var paragraph = 0;

        while (true)
        {
            var end = paragraph;

            while (end < text.Length && text[end] != '\n' && text[end] != '\r')
            {
                end++;
            }

            WrapParagraph(text, paragraph, end, font, maxWidth, segments);

            if (end >= text.Length)
            {
                return segments;
            }

            // \r\n is one break, not two empty lines.
            //
            // A text ending in a break still has one more line after it, and
            // that is where the caret sits once Enter is pressed. It falls out
            // of the loop on its own: the next pass wraps an empty range.
            paragraph = end + (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1);
        }
    }

    /// <summary>
    /// Box of the given lines, with the origin at the placement point: the
    /// caller owns the position, this only owns the size.
    /// </summary>
    public static CaptureRect Measure(IReadOnlyList<string> lines, SKFont font, int padding)
    {
        var width = 0f;

        foreach (var line in lines)
        {
            width = Math.Max(width, font.MeasureText(line));
        }

        return Box(width, lines.Count, font, padding);
    }

    /// <inheritdoc cref="Measure(IReadOnlyList{string}, SKFont, int)"/>
    public static CaptureRect Measure(IReadOnlyList<TextSegment> segments, SKFont font, int padding)
    {
        var width = 0f;

        foreach (var segment in segments)
        {
            width = Math.Max(width, font.MeasureText(segment.Text));
        }

        return Box(width, segments.Count, font, padding);
    }

    /// <summary>
    /// Where the caret goes for a position in the text, relative to the
    /// placement point.
    /// </summary>
    public static TextCaret CaretAt(IReadOnlyList<TextSegment> segments, int index, SKFont font, int padding)
    {
        // A position sitting exactly on a break belongs to the line before it,
        // which is where the caret was when the break was typed.
        var line = LineOf(segments, index);

        var offset = segments.Count == 0
            ? 0
            : Math.Clamp(index - segments[line].Start, 0, segments[line].Text.Length);

        var x = offset == 0 ? 0f : font.MeasureText(segments[line].Text.AsSpan(0, offset));

        return new TextCaret(
            padding + (int)Math.Round(x),
            padding + (int)Math.Round(line * font.Spacing),
            font.Spacing);
    }

    /// <summary>
    /// Which position in the text a point lands on. Coordinates are relative to
    /// the placement point, same as the caret.
    /// </summary>
    public static int IndexAt(IReadOnlyList<TextSegment> segments, int x, int y, SKFont font, int padding)
    {
        if (segments.Count == 0)
        {
            return 0;
        }

        var line = Math.Clamp((int)Math.Floor((y - padding) / font.Spacing), 0, segments.Count - 1);
        var segment = segments[line];
        var local = x - padding;

        if (local <= 0)
        {
            return segment.Start;
        }

        var offset = segment.Text.Length;
        var previous = 0f;

        for (var i = 1; i <= segment.Text.Length; i++)
        {
            var width = font.MeasureText(segment.Text.AsSpan(0, i));

            if (width > local)
            {
                // Whichever gap between characters is nearer: aiming at the left
                // half of a letter has to put the caret before it.
                offset = local - previous > width - local ? i : i - 1;
                break;
            }

            previous = width;
        }

        return segment.Start + offset;
    }

    /// <summary>Start of the drawn line a position sits on. What Home means.</summary>
    public static int LineStart(IReadOnlyList<TextSegment> segments, int index) =>
        segments.Count == 0 ? 0 : segments[LineOf(segments, index)].Start;

    /// <summary>End of the drawn line a position sits on. What End means.</summary>
    public static int LineEnd(IReadOnlyList<TextSegment> segments, int index) =>
        segments.Count == 0 ? 0 : segments[LineOf(segments, index)].End;

    /// <summary>
    /// The same place on the line above, or the very start of the text when
    /// there is no line above. Vertical movement is a question about pixels,
    /// not about offsets: the columns of two lines have nothing in common.
    /// </summary>
    public static int Above(IReadOnlyList<TextSegment> segments, int index, SKFont font, int padding)
    {
        if (segments.Count == 0 || LineOf(segments, index) == 0)
        {
            return 0;
        }

        var caret = CaretAt(segments, index, font, padding);

        return IndexAt(segments, caret.X, caret.Y - 1, font, padding);
    }

    /// <summary>The same place on the line below, or the very end of the text.</summary>
    public static int Below(IReadOnlyList<TextSegment> segments, int index, SKFont font, int padding)
    {
        if (segments.Count == 0)
        {
            return 0;
        }

        if (LineOf(segments, index) == segments.Count - 1)
        {
            return segments[^1].End;
        }

        var caret = CaretAt(segments, index, font, padding);

        return IndexAt(segments, caret.X, (int)Math.Ceiling(caret.Y + caret.Height) + 1, font, padding);
    }

    /// <summary>
    /// Which drawn line a position belongs to: the last one starting at or
    /// before it, same rule the caret uses.
    /// </summary>
    private static int LineOf(IReadOnlyList<TextSegment> segments, int index)
    {
        var line = 0;

        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].Start <= index)
            {
                line = i;
            }
        }

        return line;
    }

    private static CaptureRect Box(float width, int lines, SKFont font, int padding)
    {
        if (lines == 0)
        {
            return CaptureRect.Empty;
        }

        // Spacing, not the glyph box: two lines of the same text must be twice
        // as tall as one, whether or not anybody typed a descender.
        return new CaptureRect(
            0,
            0,
            (int)Math.Ceiling(width) + (padding * 2),
            (int)Math.Ceiling(font.Spacing * lines) + (padding * 2));
    }

    private static void WrapParagraph(
        string text,
        int start,
        int end,
        SKFont font,
        int? maxWidth,
        List<TextSegment> segments)
    {
        if (maxWidth is not > 0)
        {
            segments.Add(new TextSegment(text[start..end], start));
            return;
        }

        var limit = maxWidth.Value;
        var lineStart = start;
        var fitted = start;
        var i = start;

        while (i < end)
        {
            var wordStart = i;

            while (wordStart < end && text[wordStart] == ' ')
            {
                wordStart++;
            }

            var wordEnd = wordStart;

            while (wordEnd < end && text[wordEnd] != ' ')
            {
                wordEnd++;
            }

            if (wordEnd == wordStart)
            {
                // Nothing left but spaces. They stay on the last line.
                break;
            }

            if (Width(text, lineStart, wordEnd, font) <= limit)
            {
                fitted = wordEnd;
                i = wordEnd;
                continue;
            }

            if (fitted > lineStart)
            {
                segments.Add(new TextSegment(text[lineStart..fitted], lineStart));
                lineStart = wordStart;
                fitted = wordStart;

                if (Width(text, lineStart, wordEnd, font) <= limit)
                {
                    fitted = wordEnd;
                    i = wordEnd;
                    continue;
                }
            }

            lineStart = BreakWord(text, lineStart, wordEnd, font, limit, segments);
            fitted = lineStart;
            i = wordEnd;
        }

        segments.Add(new TextSegment(text[lineStart..end], lineStart));
    }

    /// <summary>
    /// Chops a run that does not fit on a line of its own, emitting every full
    /// chunk and answering with the offset the leftover starts at.
    /// </summary>
    private static int BreakWord(string text, int start, int end, SKFont font, int limit, List<TextSegment> segments)
    {
        var chunk = start;

        for (var i = start + 1; i < end; i++)
        {
            if (Width(text, chunk, i + 1, font) <= limit)
            {
                continue;
            }

            // One character always goes through even when it is wider than the
            // limit: the alternative is an empty chunk and an endless loop.
            segments.Add(new TextSegment(text[chunk..i], chunk));
            chunk = i;
        }

        return chunk;
    }

    private static float Width(string text, int start, int end, SKFont font) =>
        end <= start ? 0f : font.MeasureText(text.AsSpan(start, end - start));
}
