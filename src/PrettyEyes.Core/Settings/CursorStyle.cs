namespace PrettyEyes.Core.Settings;

/// <summary>
/// The shape the pointer takes over a frozen screen.
///
/// Taste, mostly, but not only: a crosshair that covers the pixel under it is
/// wrong for picking a colour, and a bare arrow is wrong for aiming at an edge.
/// </summary>
public enum CursorStyle
{
    /// <summary>Two unbroken lines.</summary>
    Cross,

    /// <summary>The same, opened at the middle so the aimed pixel shows through.</summary>
    Gap,

    /// <summary>A small ring and nothing else.</summary>
    Dot,

    /// <summary>Ring and four ticks around it.</summary>
    Scope,

    /// <summary>The ordinary pointer, for people who want their cursor back.</summary>
    Arrow,
}
