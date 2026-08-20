namespace PrettyEyes.Core.Tools;

/// <summary>
/// The style each tool draws with, as it is stored in the settings.
///
/// A plain dictionary rather than a property per tool: tools get added, and
/// nobody should have to touch the settings record to add one. An unknown tool
/// in the file is ignored instead of throwing - a settings file written by a
/// newer version has to stay readable by an older one.
/// </summary>
public sealed class ToolStyles
{
    private readonly Dictionary<ToolKind, ToolStyle> _styles = [];

    public ToolStyles()
    {
    }

    public ToolStyles(IReadOnlyDictionary<ToolKind, ToolStyle> stored)
    {
        foreach (var (kind, style) in stored)
        {
            if (Enum.IsDefined(kind) && style is not null)
            {
                _styles[kind] = style;
            }
        }
    }

    /// <summary>Only what differs from the default is worth writing down.</summary>
    public IReadOnlyDictionary<ToolKind, ToolStyle> Stored => _styles;

    public ToolStyle For(ToolKind kind) =>
        _styles.TryGetValue(kind, out var style) ? style : ToolStyle.Default;

    public void Set(ToolKind kind, ToolStyle style) => _styles[kind] = style;

    /// <summary>
    /// A copy, because the settings record is passed around and compared: two
    /// settings holding the same mutable dictionary would look equal after one
    /// of them changed.
    /// </summary>
    public ToolStyles Copy() => new(_styles);
}
