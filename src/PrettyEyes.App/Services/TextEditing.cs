using PrettyEyes.App.Views;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Text;
using PrettyEyes.Core.Tools;

namespace PrettyEyes.App.Services;

/// <summary>
/// One label being typed: the buffer, where it sits, and which window holds the
/// caret.
///
/// The window matters and the focus does not. The session activates the first
/// overlay only, and a click on the toolbar moves the keyboard to whichever
/// window the toolbar is in; on two monitors that is not the window the caret
/// is in. Keys are therefore answered here, by the session, wherever they land.
/// </summary>
internal sealed class TextEditing
{
    public required OverlayWindow Window { get; init; }

    public required TextEditor Editor { get; init; }

    /// <summary>Top-left of the label's box, in virtual desktop pixels.</summary>
    public required int X { get; init; }

    public required int Y { get; init; }

    /// <summary>
    /// The width the text wraps at. Never null in practice: a click wraps at
    /// the right edge of the selected region, because a line running off the
    /// monitor is not a label.
    /// </summary>
    public required int? MaxWidth { get; init; }

    public required ToolStyle Style { get; init; }

    /// <summary>
    /// The label this is a second pass over, or null for a new one. It stays in
    /// the document while it is edited, detached so that it is not drawn twice.
    /// </summary>
    public TextAnnotation? Original { get; init; }

    /// <summary>Half of the blink. Flipped by the session's timer.</summary>
    public bool CaretOn { get; set; } = true;

    /// <summary>The label as it would be if committed right now.</summary>
    public TextAnnotation Label => new(Editor.Text, X, Y, MaxWidth, Style);
}
