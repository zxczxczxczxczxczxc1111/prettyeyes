using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Stats;

namespace PrettyEyes.App.Services;

/// <summary>
/// Counts screenshots and keeps the number on disk.
///
/// Written straight through rather than buffered until exit: a tray
/// application is closed by the task manager as often as by its own menu, and
/// a counter that only survives a polite exit is worse than none. The file is
/// a few hundred bytes, so the write costs less than the render that preceded
/// it.
/// </summary>
public sealed class ShotCounter
{
    private readonly JsonStatsStore _store;
    private readonly object _gate = new();

    private ShotStats _stats;

    public ShotCounter(JsonStatsStore store)
    {
        _store = store;
        _stats = store.Load();
    }

    public ShotStats Current
    {
        get
        {
            lock (_gate)
            {
                return _stats;
            }
        }
    }

    /// <summary>Shots taken today and on the six days before it.</summary>
    public int ThisWeek => Current.Since(DateOnly.FromDateTime(DateTime.Now).AddDays(-6));

    public void Record(ShotTarget target)
    {
        lock (_gate)
        {
            _stats = _stats.Record(target, DateOnly.FromDateTime(DateTime.Now));

            if (!_store.Save(_stats))
            {
                // Worth a line and nothing more: the number in the settings
                // window is the least important thing the application does.
                Log.Default.Info("счётчик снимков не сохранился");
            }
        }
    }
}
