using System.Collections.Concurrent;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.Core.Capture;

/// <summary>
/// Hands each monitor to the first painter willing to take it.
///
/// Two kinds of no, and telling them apart is the whole point of this class.
/// A <see cref="NotSupportedException"/> is a verdict about this monitor: a
/// rotated output, a desktop format we do not read, an adapter that refuses to
/// duplicate. It is remembered, because asking again every screenshot costs
/// time and always gets the same answer. Anything else is bad luck: a mode
/// change, a game taking the screen, a UAC prompt. It is forgotten
/// immediately, because latching the application onto the older engine for a
/// passing accident brings the yellow border back until the next restart.
/// </summary>
public sealed class PainterChain : IDisposable
{
    private readonly IReadOnlyList<IMonitorPainter> _painters;

    /// <summary>
    /// Where the refusals are written down. Injected rather than taken from
    /// Log.Default because the tests of this class run with fake painters and
    /// fake monitors, and their chatter was landing in the real user's log
    /// file, where it read like a broken installation.
    /// </summary>
    private readonly Log _log;

    /// <summary>Monitors a painter has refused, by painter name.</summary>
    private readonly ConcurrentDictionary<(string Painter, string Monitor, CaptureRect Bounds), bool> _refused = new();

    private readonly ConcurrentDictionary<string, string> _assignments = new();

    private bool _disposed;

    public PainterChain(IReadOnlyList<IMonitorPainter> painters, Log? log = null)
    {
        _log = log ?? Log.Default;

        if (painters.Count == 0)
        {
            throw new ArgumentException("A chain with no painters can capture nothing.", nameof(painters));
        }

        _painters = painters;
    }

    /// <summary>
    /// Which painter did which monitor last time. Written to the log once a
    /// run: when somebody reports the border is back, this is the first thing
    /// worth knowing.
    /// </summary>
    public IReadOnlyDictionary<string, string> Assignments => _assignments;

    /// <summary>
    /// Forgets monitors that are no longer there. Without this a screen that
    /// was unplugged keeps showing up in the log line about who paints what,
    /// which is the one line somebody will read when the border comes back.
    /// </summary>
    public void KeepOnly(IEnumerable<string> monitors)
    {
        var alive = monitors.ToHashSet(StringComparer.Ordinal);

        foreach (var gone in _assignments.Keys.Where(id => !alive.Contains(id)).ToArray())
        {
            _assignments.TryRemove(gone, out _);
        }
    }

    public void Paint(MonitorInfo monitor, IntPtr destination, int stride)
    {
        Exception? lastFailure = null;

        foreach (var painter in _painters)
        {
            var verdict = (painter.Name, monitor.DeviceId, monitor.Bounds);

            if (_refused.ContainsKey(verdict))
            {
                continue;
            }

            try
            {
                painter.Paint(monitor, destination, stride);
                _assignments[monitor.DeviceId] = painter.Name;

                return;
            }
            catch (NotSupportedException refusal)
            {
                _refused[verdict] = true;
                lastFailure ??= refusal;
                _log.Info(
                    $"маляр {painter.Name} отказался от {monitor.DeviceId} насовсем: {refusal.Message}");
            }
            catch (Exception failure)
            {
                // Later painters are more likely to have something to say about
                // why the capture died, so the newest reason wins.
                lastFailure = failure;

                // Logged every time and not once: a painter that quietly loses
                // every second screenshot looks exactly like a working
                // application until somebody counts.
                _log.Info(
                    $"маляр {painter.Name} не смог {monitor.DeviceId} в этот раз: {failure.Message}");
            }
        }

        throw new InvalidOperationException(
            $"No capture engine could paint monitor {monitor.DeviceId}.", lastFailure);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var painter in _painters)
        {
            painter.Dispose();
        }
    }
}
