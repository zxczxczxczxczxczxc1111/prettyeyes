namespace PrettyEyes.Core.Text;

/// <summary>
/// One drawn line of a label, and where it starts in the text it came from.
///
/// The offset is the whole point: without it there is no way to answer "which
/// character is under the pointer" or "where does the caret go", because a line
/// break inserted by wrapping has no character of its own to count.
/// </summary>
public readonly record struct TextSegment(string Text, int Start)
{
    /// <summary>One past the last character, in the original text.</summary>
    public int End => Start + Text.Length;
}

/// <summary>Where the caret is drawn, relative to the placement point.</summary>
public readonly record struct TextCaret(int X, int Y, float Height);
