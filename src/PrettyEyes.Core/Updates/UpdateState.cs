namespace PrettyEyes.Core.Updates;

/// <summary>What the update is doing right now, for the one line that says so.</summary>
public enum UpdateStage
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Installing,
    Failed,
}

/// <summary>
/// The whole state in one value. Handed out rather than exposed as a handful of
/// properties: a status line built from three fields read at three moments can
/// show a combination that never existed.
///
/// Lives in Core rather than beside the service that raises it, because more
/// than one thing now reads it - a window, a tray icon - and the rules for
/// what each of them shows are worth testing without an application around
/// them.
/// </summary>
public sealed record UpdateState(UpdateStage Stage, ReleaseVersion? Version = null, double Progress = 0);
