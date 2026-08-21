using System.Text;
using SkiaSharp;

namespace PrettyEyes.Core.Text;

/// <summary>
/// The text being typed, the caret and the selection. Knows nothing about
/// fonts, lines or pixels: everything here is offsets into a string, which is
/// what makes it testable without an overlay on screen.
///
/// Its history is its own and dies with the label. While the caret is up,
/// Ctrl+Z has to take back the last word rather than the last shape drawn on
/// the screenshot, and mixing the two into one stack means undo does whichever
/// the user did not mean.
/// </summary>
public sealed class TextEditor
{
    /// <summary>
    /// Enough for anything anybody labels a screenshot with. The point is to
    /// bound the layout work, which is redone on every keystroke.
    /// </summary>
    public const int MaxLength = 2000;

    /// <summary>Deep enough for a long burst of typing, shallow enough to bound.</summary>
    private const int HistoryDepth = 200;

    /// <summary>U+FFFD, the sign for "this could not be rendered".</summary>
    private const char Replacement = '�';

    private readonly List<(string Text, int Caret, int Anchor)> _history = [];

    /// <summary>
    /// Whether the next single character can join the entry already on top of
    /// the history instead of opening a new one. Otherwise every letter is its
    /// own undo step, and taking back a sentence is a drum solo.
    /// </summary>
    private bool _burst;

    public TextEditor(string text = "")
    {
        Text = text;

        // At the end: editing an existing label starts where it was left off,
        // and a new one has nowhere else to start.
        Caret = text.Length;
        Anchor = Caret;
    }

    public string Text { get; private set; }

    public int Caret { get; private set; }

    /// <summary>Where the selection was started. Equal to the caret when there is none.</summary>
    public int Anchor { get; private set; }

    public bool HasSelection => Caret != Anchor;

    public int SelectionStart => Math.Min(Caret, Anchor);

    public int SelectionLength => Math.Abs(Caret - Anchor);

    /// <summary>
    /// Strips what must never reach the screenshot: line endings are normalised
    /// to plain newlines, control characters go away entirely, and a codepoint
    /// the font has no glyph for becomes a single replacement sign.
    ///
    /// Colour emoji are the case that matters. They have their own tool, and
    /// letting one through here paints a black box on somebody's screenshot.
    /// </summary>
    public static string Sanitize(string chunk, SKFont font)
    {
        var cleaned = new StringBuilder(chunk.Length);

        for (var i = 0; i < chunk.Length; i++)
        {
            var glyph = chunk[i];

            if (glyph == '\r')
            {
                cleaned.Append('\n');

                if (i + 1 < chunk.Length && chunk[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            if (glyph == '\n')
            {
                cleaned.Append('\n');
                continue;
            }

            if (char.IsControl(glyph))
            {
                continue;
            }

            // A surrogate pair is one codepoint and must be judged as one, or
            // half of an emoji survives as a lone surrogate.
            var pair = char.IsHighSurrogate(glyph) && i + 1 < chunk.Length && char.IsLowSurrogate(chunk[i + 1]);
            var codepoint = pair ? char.ConvertToUtf32(glyph, chunk[i + 1]) : glyph;

            cleaned.Append(font.Typeface.GetGlyph(codepoint) == 0 ? Replacement : pair ? chunk.Substring(i, 2) : glyph.ToString());

            if (pair)
            {
                i++;
            }
        }

        return cleaned.ToString();
    }

    /// <summary>
    /// Puts text in, replacing the selection. The caller sanitizes first when
    /// the text came from a keyboard or a clipboard; this only guards the
    /// length and the line endings, because those it can guard without a font.
    /// </summary>
    public void Insert(string chunk)
    {
        var text = chunk.Replace("\r\n", "\n").Replace('\r', '\n');
        var cleaned = new StringBuilder(text.Length);

        foreach (var glyph in text)
        {
            if (glyph == '\n' || !char.IsControl(glyph))
            {
                cleaned.Append(glyph);
            }
        }

        text = cleaned.ToString();

        if (text.Length == 0 && !HasSelection)
        {
            return;
        }

        // One character with nothing special about it continues the burst. A
        // space, a newline or a whole pasted block ends it.
        var joins = _burst && text.Length == 1 && !char.IsWhiteSpace(text[0]);

        Remember(joins);
        _burst = text.Length == 1 && !char.IsWhiteSpace(text[0]);

        var start = SelectionStart;
        var room = MaxLength - (Text.Length - SelectionLength);

        if (text.Length > room)
        {
            text = Cut(text, Math.Max(0, room));
        }

        Text = string.Concat(Text.AsSpan(0, start), text, Text.AsSpan(start + SelectionLength));
        Caret = start + text.Length;
        Anchor = Caret;
    }

    public void Backspace()
    {
        if (!HasSelection && Caret == 0)
        {
            return;
        }

        Remember(joins: false);
        _burst = false;

        if (HasSelection)
        {
            DeleteRange(SelectionStart, SelectionLength);
            return;
        }

        // A surrogate pair is one character to everybody but the string.
        var width = Caret >= 2 && char.IsLowSurrogate(Text[Caret - 1]) && char.IsHighSurrogate(Text[Caret - 2]) ? 2 : 1;

        DeleteRange(Caret - width, width);
    }

    public void Delete()
    {
        if (!HasSelection && Caret >= Text.Length)
        {
            return;
        }

        Remember(joins: false);
        _burst = false;

        if (HasSelection)
        {
            DeleteRange(SelectionStart, SelectionLength);
            return;
        }

        var width = Caret + 1 < Text.Length && char.IsHighSurrogate(Text[Caret]) && char.IsLowSurrogate(Text[Caret + 1])
            ? 2
            : 1;

        DeleteRange(Caret, width);
    }

    /// <summary>
    /// Puts the caret somewhere. Extending keeps the anchor where it was, which
    /// is what Shift and a mouse drag both mean.
    /// </summary>
    public void MoveTo(int index, bool extend)
    {
        _burst = false;
        Caret = Math.Clamp(index, 0, Text.Length);

        if (!extend)
        {
            Anchor = Caret;
        }
    }

    /// <summary>
    /// One character left or right. Without Shift and with something selected,
    /// this collapses to the near end rather than moving, same as everywhere.
    /// </summary>
    public void MoveBy(int step, bool extend)
    {
        if (!extend && HasSelection)
        {
            MoveTo(step < 0 ? SelectionStart : SelectionStart + SelectionLength, extend: false);
            return;
        }

        var index = Caret + step;

        // Step over a surrogate pair in one go.
        if (step < 0 && Caret >= 2 && char.IsLowSurrogate(Text[Caret - 1]) && char.IsHighSurrogate(Text[Caret - 2]))
        {
            index = Caret - 2;
        }
        else if (step > 0 && Caret + 1 < Text.Length && char.IsHighSurrogate(Text[Caret]) && char.IsLowSurrogate(Text[Caret + 1]))
        {
            index = Caret + 2;
        }

        MoveTo(index, extend);
    }

    public void SelectAll()
    {
        _burst = false;
        Anchor = 0;
        Caret = Text.Length;
    }

    /// <summary>
    /// One step back through this label's own typing. False when there is
    /// nothing left, which is the moment the overlay hands Ctrl+Z back to the
    /// document.
    /// </summary>
    public bool Undo()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        (Text, Caret, Anchor) = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _burst = false;

        return true;
    }

    private void DeleteRange(int start, int length)
    {
        Text = Text.Remove(start, length);
        Caret = start;
        Anchor = start;
    }

    private void Remember(bool joins)
    {
        if (joins && _history.Count > 0)
        {
            return;
        }

        _history.Add((Text, Caret, Anchor));

        if (_history.Count > HistoryDepth)
        {
            _history.RemoveAt(0);
        }
    }

    /// <summary>
    /// Trims to a length without leaving half of a surrogate pair behind: a
    /// lone surrogate is not a character and renders as garbage.
    /// </summary>
    private static string Cut(string text, int length)
    {
        if (length >= text.Length)
        {
            return text;
        }

        while (length > 0 && char.IsLowSurrogate(text[length]))
        {
            length--;
        }

        return text[..length];
    }
}
