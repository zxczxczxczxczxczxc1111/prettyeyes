using PrettyEyes.Core.Stats;
using Xunit;

namespace PrettyEyes.Core.Tests.Stats;

public class JsonStatsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "prettyeyes-tests",
        Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_folder, "stats.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void A_file_that_is_not_there_yet_reads_as_no_counts()
    {
        Assert.Equal(0, new JsonStatsStore(Path_).Load().Total);
    }

    [Fact]
    public void What_was_saved_comes_back()
    {
        var store = new JsonStatsStore(Path_);
        var day = new DateOnly(2026, 8, 22);

        store.Save(ShotStats.Empty.Record(ShotTarget.Clipboard, day).Record(ShotTarget.Pin, day));

        var back = store.Load();

        Assert.Equal(1, back.ToClipboard);
        Assert.Equal(1, back.ToPin);
        Assert.Equal(new[] { new DayCount(day, 2) }, back.Days);
    }

    [Fact]
    public void A_broken_file_reads_as_no_counts_instead_of_throwing()
    {
        // A counter is not worth a failed start-up, and the next save fixes it.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, "{ this is not json");

        Assert.Equal(0, new JsonStatsStore(Path_).Load().Total);
    }

    [Fact]
    public void The_total_is_not_written_down()
    {
        // It is the sum of the three counters, and a stored copy is one more
        // thing that can disagree with them.
        var store = new JsonStatsStore(Path_);
        store.Save(ShotStats.Empty.Record(ShotTarget.File, new DateOnly(2026, 8, 22)));

        Assert.DoesNotContain("Total", File.ReadAllText(Path_), StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_creates_the_folder_it_needs()
    {
        Assert.True(new JsonStatsStore(Path_).Save(ShotStats.Empty));
        Assert.True(File.Exists(Path_));
    }
}
