namespace PrettyEyes.Core.Platform;

public interface IScreenCapture
{
    /// <summary>
    /// Grabs every monitor into a single image in virtual-desktop coordinates.
    /// </summary>
    CaptureResult CaptureAll();
}
