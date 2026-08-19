namespace PrettyEyes.Core.Geometry;

/// <summary>
/// One physical display. Bounds are in physical pixels of the virtual desktop.
/// Scale is carried for the UI layer only - the model never applies it.
/// </summary>
public sealed record MonitorInfo(string DeviceId, CaptureRect Bounds, double Scale);
