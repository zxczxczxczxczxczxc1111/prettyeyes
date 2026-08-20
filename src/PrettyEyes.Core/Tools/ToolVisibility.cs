namespace PrettyEyes.Core.Tools;

/// <summary>
/// Which tools the toolbar shows.
///
/// Stored as the exceptions rather than the full list: a tool added in a later
/// version is shown by default, which is the only sensible answer for a setting
/// that could not have been made about it.
/// </summary>
public sealed class ToolVisibility
{
    private readonly Dictionary<ToolKind, bool> _shown;

    public ToolVisibility(Dictionary<ToolKind, bool>? shown = null) =>
        _shown = shown is null ? [] : new Dictionary<ToolKind, bool>(shown);

    /// <summary>Every tool there is, in the order the toolbar puts them.</summary>
    public static IReadOnlyList<ToolKind> All { get; } =
        [ToolKind.Blur, ToolKind.Arrow, ToolKind.Line, ToolKind.Emoji, ToolKind.Rectangle];

    public bool IsShown(ToolKind kind) => !_shown.TryGetValue(kind, out var shown) || shown;

    public int ShownCount => All.Count(IsShown);

    /// <summary>
    /// Turns one tool on or off. Refuses to turn off the last one: a toolbar
    /// with nothing to draw with is a toolbar with a copy button, and getting
    /// out of that state would mean going back to the settings to guess what
    /// went wrong.
    /// </summary>
    public bool TrySet(ToolKind kind, bool shown)
    {
        if (!shown && IsShown(kind) && ShownCount <= 1)
        {
            return false;
        }

        _shown[kind] = shown;

        return true;
    }

    /// <summary>What goes into the settings file.</summary>
    public Dictionary<ToolKind, bool> ToDictionary() => new(_shown);
}
