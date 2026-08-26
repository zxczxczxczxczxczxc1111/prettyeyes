namespace PrettyEyes.Core.Tools;

/// <summary>
/// The tool a capture starts with, if the user picked one.
///
/// The whole rule is "only on the edge": the toolbar appearing is a moment, and
/// the event that carries it fires again on every later drag of the frame.
/// </summary>
public static class DefaultTool
{
    public static ToolKind? Apply(ToolKind? configured, bool toolbarWasShown)
        => toolbarWasShown ? null : configured;
}

/// <summary>
/// Whether the toolbar rides along with the frame while it is being changed.
/// </summary>
public static class ToolbarDrag
{
    /// <param name="freshDrag">
    /// A selection drawn from nothing, as opposed to the existing frame being
    /// pulled by a grip. The two look the same to the session and have to be
    /// told apart here: a new frame is the panel appearing again, and that is a
    /// moment, not a journey.
    /// </param>
    public static bool FollowsFrame(bool toolbarShown, bool freshDrag) =>
        toolbarShown && !freshDrag;
}
