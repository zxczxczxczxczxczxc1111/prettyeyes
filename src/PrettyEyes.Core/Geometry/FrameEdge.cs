namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Where the bottom line of the selection frame may be drawn.
///
/// The pixels themselves are captured from rcMonitor, so nothing is lost down
/// there: this is about the line being visible, not about the shot.
/// </summary>
public static class FrameEdge
{
    public static int Bottom(int selectionBottom, int usableBottom, int monitorBottom)
        => Math.Min(selectionBottom, Math.Min(usableBottom, monitorBottom - 1) - 1);
}
