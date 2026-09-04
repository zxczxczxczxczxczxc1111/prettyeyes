using PrettyEyes.Core.Settings;

namespace PrettyEyes.Core.Capture;

/// <summary>
/// What order the painters are offered a monitor in.
/// </summary>
public static class CaptureOrder
{
    /// <summary>
    /// The painter names, kept here because three places need to agree on them:
    /// the painters themselves, the setting that picks one, and the log line
    /// that says who painted what.
    /// </summary>
    public const string Duplication = "дублирование выхода";

    public const string WindowsGraphicsCapture = "Windows.Graphics.Capture";

    public const string Gdi = "GDI";

    /// <summary>
    /// The chain with the chosen engine moved to the front, or the chain
    /// untouched when nothing was chosen.
    ///
    /// Nothing is ever removed. A choice is about who goes first, not about who
    /// is allowed to exist: an engine that refuses one monitor has to leave the
    /// others something to fall back to, and a person who picks an engine their
    /// Windows does not have should still get a screenshot.
    /// </summary>
    public static IReadOnlyList<string> Of(CaptureSource source, IReadOnlyList<string> painters)
    {
        var first = NameOf(source);

        if (first is null || !painters.Contains(first))
        {
            return painters;
        }

        return [first, .. painters.Where(painter => painter != first)];
    }

    /// <summary>Null for <see cref="CaptureSource.Auto"/>: nobody was chosen.</summary>
    private static string? NameOf(CaptureSource source) => source switch
    {
        CaptureSource.WindowsGraphicsCapture => WindowsGraphicsCapture,
        CaptureSource.Gdi => Gdi,
        _ => null,
    };
}
