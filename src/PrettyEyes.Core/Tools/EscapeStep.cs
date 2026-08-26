namespace PrettyEyes.Core.Tools;

/// <summary>
/// What Escape takes back when a tool is in hand. Lives in Core so the rule can
/// be tested without spinning up a window, the same reason ToolSelection and
/// DefaultTool live here.
/// </summary>
public static class EscapeStep
{
    /// <summary>
    /// Whether Escape should drop the tool instead of closing the overlay.
    ///
    /// A default tool over an empty canvas is the one case where it should not:
    /// nobody picked that tool, and unarming it was a rung on the ladder the
    /// user never climbed on purpose. It cost a second Escape on every capture.
    ///
    /// Everything else keeps the old behaviour, and deliberately so. With a
    /// tool armed the frame cannot be dragged at all - the drawing gesture eats
    /// the press before the grips are ever tested - so this is how a tool gets
    /// put down, and closing the capture instead would be a theft.
    /// </summary>
    public static bool ClearsTool(bool pickedByHand, bool hasAnnotations) =>
        pickedByHand || hasAnnotations;
}
