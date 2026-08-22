using PrettyEyes.Core.Stats;
using Xunit;

namespace PrettyEyes.Core.Tests.Stats;

public class ShotStatsTests
{
    private static readonly DateOnly Today = new(2026, 8, 22);

    [Fact]
    public void A_fresh_count_is_all_zeroes()
    {
        var stats = ShotStats.Empty;

        Assert.Equal(0, stats.Total);
        Assert.Empty(stats.Days);
    }

    [Fact]
    public void A_shot_counts_towards_its_target_and_towards_everything()
    {
        var stats = ShotStats.Empty.Record(ShotTarget.Clipboard, Today);

        Assert.Equal(1, stats.ToClipboard);
        Assert.Equal(0, stats.ToFile);
        Assert.Equal(1, stats.Total);
    }

    [Fact]
    public void Two_shots_on_one_day_share_a_day()
    {
        var stats = ShotStats.Empty
            .Record(ShotTarget.File, Today)
            .Record(ShotTarget.Pin, Today);

        Assert.Equal(new[] { new DayCount(Today, 2) }, stats.Days);
    }

    [Fact]
    public void Shots_on_different_days_are_counted_apart()
    {
        var yesterday = Today.AddDays(-1);

        var stats = ShotStats.Empty
            .Record(ShotTarget.File, yesterday)
            .Record(ShotTarget.File, Today);

        Assert.Equal(2, stats.Days.Count);
        Assert.Equal(1, stats.Days.Single(day => day.Day == yesterday).Count);
    }

    [Fact]
    public void The_week_counts_today_and_the_six_days_before_it()
    {
        var stats = ShotStats.Empty
            .Record(ShotTarget.File, Today)
            .Record(ShotTarget.File, Today.AddDays(-6))
            .Record(ShotTarget.File, Today.AddDays(-7));

        Assert.Equal(2, stats.Since(Today.AddDays(-6)));
    }

    [Fact]
    public void Days_older_than_a_month_are_forgotten()
    {
        // The file is a counter, not an archive: without a limit it grows a
        // line a day forever and nothing ever reads the old ones.
        var stats = ShotStats.Empty
            .Record(ShotTarget.File, Today.AddDays(-40))
            .Record(ShotTarget.File, Today);

        Assert.Equal(new[] { new DayCount(Today, 1) }, stats.Days);
    }

    [Fact]
    public void The_totals_survive_a_day_being_forgotten()
    {
        // Only the daily breakdown is trimmed. "Two thousand screenshots since
        // you installed it" is the number worth keeping.
        var stats = ShotStats.Empty
            .Record(ShotTarget.File, Today.AddDays(-40))
            .Record(ShotTarget.File, Today);

        Assert.Equal(2, stats.Total);
    }

    [Fact]
    public void Recording_leaves_the_original_count_alone()
    {
        var before = ShotStats.Empty.Record(ShotTarget.Pin, Today);

        before.Record(ShotTarget.Pin, Today);

        Assert.Equal(1, before.Total);
    }
}
