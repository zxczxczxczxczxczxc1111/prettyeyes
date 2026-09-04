namespace PrettyEyes.Core.Settings;

/// <summary>
/// Which engine gets first refusal on every monitor.
///
/// There is deliberately no automatic detection behind this. Two attempts at
/// one were measured on 04.09.2026 against a laptop that hands back a picture
/// with the gamma stripped out of it, and both missed: the desktop format it
/// reports is the ordinary one, and so is the colour space its output reports.
/// A machine that lies in both of the places you can politely ask cannot be
/// caught by asking, and catching it by comparing pictures buys a second bug
/// for every machine where nothing is wrong.
///
/// So the choice is handed over. The fault is rare, the symptom is impossible
/// to miss, and the person looking at a dark screenshot is better placed to
/// pick than any heuristic written for a machine nobody here owns.
/// </summary>
public enum CaptureSource
{
    /// <summary>The chain as it ships: duplication, then WGC, then GDI.</summary>
    Auto,

    /// <summary>
    /// Windows.Graphics.Capture first. Slower and Windows may draw its yellow
    /// capture border, but the conversion is done by the system, so a desktop
    /// duplication reads wrongly comes out right.
    /// </summary>
    WindowsGraphicsCapture,

    /// <summary>
    /// GDI first. Asks nothing of the system and works where the other two do
    /// not, at the price of hardware-accelerated and protected windows coming
    /// out black.
    /// </summary>
    Gdi,
}
