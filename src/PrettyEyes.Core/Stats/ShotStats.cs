namespace PrettyEyes.Core.Stats;

/// <summary>Where a screenshot ended up.</summary>
public enum ShotTarget
{
    Clipboard,
    File,
    Pin,
}

/// <summary>How many shots one day carried.</summary>
public readonly record struct DayCount(DateOnly Day, int Count);

/// <summary>
/// How much the application has been used. Deliberately small and deliberately
/// not part of the settings: settings are rewritten whole and carry a schema
/// with migrations, while this changes on every single screenshot, and losing
/// it should cost nothing at all.
///
/// Today is passed in rather than read from the clock, because a counter that
/// asks the system what day it is cannot be tested at a month boundary.
/// </summary>
public sealed record ShotStats(int ToClipboard, int ToFile, int ToPin, IReadOnlyList<DayCount> Days)
{
    /// <summary>
    /// A month of daily numbers is enough for "this week" and for a sense of
    /// the last few weeks. Everything older is a line a day that nobody reads.
    /// </summary>
    private const int DaysKept = 30;

    public static ShotStats Empty { get; } = new(0, 0, 0, []);

    public int Total => ToClipboard + ToFile + ToPin;

    public ShotStats Record(ShotTarget target, DateOnly today)
    {
        var days = Days
            .Where(day => day.Day > today.AddDays(-DaysKept))
            .ToList();

        var at = days.FindIndex(day => day.Day == today);

        if (at < 0)
        {
            days.Add(new DayCount(today, 1));
        }
        else
        {
            days[at] = days[at] with { Count = days[at].Count + 1 };
        }

        return new ShotStats(
            ToClipboard + (target == ShotTarget.Clipboard ? 1 : 0),
            ToFile + (target == ShotTarget.File ? 1 : 0),
            ToPin + (target == ShotTarget.Pin ? 1 : 0),
            days);
    }

    /// <summary>Shots taken on <paramref name="from"/> or later.</summary>
    public int Since(DateOnly from) => Days.Where(day => day.Day >= from).Sum(day => day.Count);
}
