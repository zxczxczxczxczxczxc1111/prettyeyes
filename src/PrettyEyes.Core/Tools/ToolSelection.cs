namespace PrettyEyes.Core.Tools;

/// <summary>
/// Which tool a click lands on. Lives in Core so the rule can be tested without
/// spinning up a window, which is how the "one click does nothing" bug got to
/// ship in the first place.
/// </summary>
public static class ToolSelection
{
    public static ToolKind? Next(ToolKind? current, ToolKind clicked)
        => current == clicked ? null : clicked;
}
