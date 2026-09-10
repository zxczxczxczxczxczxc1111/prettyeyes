using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Waiting for the screen to actually look the way it was told to.
/// </summary>
public static class Composition
{
    /// <summary>
    /// Returns once what is on the monitors matches what the windows were last
    /// told. Two frames rather than one: the first call returns at the end of
    /// the frame already in flight, which may have been composed before the
    /// change arrived.
    ///
    /// Costs up to two refreshes - a third of a frame budget each at 60 Hz -
    /// and is only ever called on the way into a capture.
    /// </summary>
    public static void Settle()
    {
        NativeMethods.DwmFlush();
        NativeMethods.DwmFlush();
    }
}
