namespace PrettyEyes.Core.Geometry;

/// <summary>
/// One physical display. Bounds are in physical pixels of the virtual desktop.
/// Scale is carried for the UI layer only - the model never applies it.
///
/// WorkArea is the same rectangle minus the taskbar. Optional because only the
/// UI cares: capture works from Bounds and would be wrong to crop.
/// </summary>
public sealed record MonitorInfo(
    string DeviceId,
    CaptureRect Bounds,
    double Scale,
    CaptureRect? WorkArea = null)
{
    /// <summary>Where panels are allowed to sit: the work area when the shell
    /// told us one, the whole monitor otherwise.</summary>
    public CaptureRect Usable => WorkArea ?? Bounds;
}
