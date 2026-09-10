namespace PrettyEyes.Core.Updates;

/// <summary>
/// Whether the tray icon wears a mark, and what it says when hovered.
///
/// A decision rather than a flag somebody sets: the mark means "there is a
/// release you have not installed", which is a fact about the state, not about
/// what happened most recently. Written down here so it can be tested without
/// a tray, a window or a network.
/// </summary>
public static class UpdateBadge
{
    /// <summary>
    /// Shell_NotifyIcon takes 128 characters of tip including the terminator,
    /// and quietly cuts anything longer - which would take the version off the
    /// end, the one part worth reading.
    /// </summary>
    private const int TipLimit = 127;

    public static bool ShouldShow(UpdateState state) =>
        state.Version is not null
        && state.Stage is UpdateStage.Available
            or UpdateStage.Downloading
            or UpdateStage.Installing
            // A download that broke halfway leaves the release exactly where it
            // was. This is the failure most likely to work on a second try, so
            // it is the last one that should quietly stop mentioning itself.
            or UpdateStage.Failed;

    /// <summary>
    /// What the icon says when the pointer rests on it. The name alone when
    /// there is nothing to report, the name and the waiting version when there
    /// is.
    /// </summary>
    public static string Tooltip(string name, UpdateState state)
    {
        if (!ShouldShow(state))
        {
            return Fit(name);
        }

        var tail = $": доступна {state.Version}";

        // The name gives way, not the version: somebody hovering a tray icon
        // already knows which application it is.
        return Fit(name, TipLimit - tail.Length) + tail;
    }

    private static string Fit(string text, int limit = TipLimit) =>
        text.Length <= limit ? text : text[..Math.Max(limit, 0)];
}
