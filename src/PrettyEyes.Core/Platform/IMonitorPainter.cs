using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Platform;

/// <summary>
/// Paints one monitor into a buffer that belongs to somebody else.
///
/// Capture used to be one engine grabbing the whole desktop. It is split per
/// monitor because the engines have different refusals: Desktop Duplication
/// will not touch a rotated output, GDI cannot see hardware-accelerated
/// windows, and taking the whole screenshot back to the older engine because
/// of one monitor would bring the yellow capture border back on all of them.
///
/// Paint is called from several threads at once, one per monitor. Anything an
/// implementation shares between monitors has to survive that.
/// </summary>
public interface IMonitorPainter : IDisposable
{
    /// <summary>Shown in the log, so it is Russian and readable.</summary>
    string Name { get; }

    /// <summary>
    /// Writes the monitor into the buffer, row by row, starting at
    /// <paramref name="destination"/> and stepping <paramref name="stride"/>
    /// bytes per row.
    ///
    /// Throws <see cref="NotSupportedException"/> to mean "this monitor is not
    /// mine and never will be while it stays like this". Any other exception
    /// means bad luck this time.
    /// </summary>
    void Paint(MonitorInfo monitor, IntPtr destination, int stride);

    /// <summary>
    /// Lets go of everything expensive; the next Paint builds it again.
    ///
    /// Called after a long idle spell. Most painters hold nothing worth
    /// releasing, which is why doing nothing is the default.
    /// </summary>
    void Release()
    {
    }
}
