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
