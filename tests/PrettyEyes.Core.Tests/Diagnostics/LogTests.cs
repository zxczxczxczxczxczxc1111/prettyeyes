using PrettyEyes.Core.Diagnostics;
using Xunit;

namespace PrettyEyes.Core.Tests.Diagnostics;

public class LogTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"pe-log-{Guid.NewGuid()}", "log.txt");

    [Fact]
    public void Line_carries_a_timestamp_the_level_and_the_message()
    {
        var path = TempPath();

        new Log(path).Info("снимок готов");

        var line = File.ReadAllLines(path).Single();

        // 2026-08-20 01:02:03.123 INFO снимок готов
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} INFO снимок готов$", line);
    }

    [Fact]
    public void Error_records_the_exception_type_and_message()
    {
        var path = TempPath();

        new Log(path).Error("копирование", new InvalidOperationException("буфер занят"));

        var text = File.ReadAllText(path);

        Assert.Contains("ERROR копирование", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("буфер занят", text);
    }

    [Fact]
    public void Scope_writes_how_long_the_work_took()
    {
        var path = TempPath();
        var log = new Log(path);

        using (log.Scope("capture"))
        {
            Thread.Sleep(5);
        }

        var line = File.ReadAllLines(path).Single();

        Assert.Matches(@"INFO capture: \d+(\.\d+)? мс$", line);
    }

    [Fact]
    public void Oversized_file_rolls_over_and_keeps_one_previous_copy()
    {
        var path = TempPath();
        var log = new Log(path, maxBytes: 1024);

        // Well past the limit: the roll happens on the write that crosses it.
        for (var i = 0; i < 60; i++)
        {
            log.Info(new string('x', 40));
        }

        var rolled = Path.Combine(Path.GetDirectoryName(path)!, "log.1.txt");

        Assert.True(File.Exists(rolled), "старый журнал должен уезжать в log.1.txt");

        // The roll is decided before the write, so the file may end one line
        // past the limit. Trimming that last line would mean rewriting the
        // file on every append, which is a worse trade.
        Assert.True(
            new FileInfo(path).Length <= 1024 + 200,
            "текущий журнал не должен уходить дальше предела больше чем на строку");
    }

    [Fact]
    public void Second_rollover_overwrites_the_previous_copy_instead_of_piling_up()
    {
        var path = TempPath();
        var log = new Log(path, maxBytes: 512);

        for (var i = 0; i < 200; i++)
        {
            log.Info(new string('x', 40));
        }

        var files = Directory.GetFiles(Path.GetDirectoryName(path)!);

        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void Unreachable_path_is_swallowed_because_logging_must_never_break_the_app()
    {
        // A file where a directory has to be: creating the folder is impossible.
        var blocker = Path.Combine(Path.GetTempPath(), $"pe-log-{Guid.NewGuid()}");
        File.WriteAllText(blocker, "я не папка");

        var log = new Log(Path.Combine(blocker, "log.txt"));

        log.Info("сообщение в никуда");
        log.Error("и это тоже", new InvalidOperationException("бух"));
    }

    [Fact]
    public void Writes_from_several_threads_do_not_lose_lines()
    {
        var path = TempPath();
        var log = new Log(path);

        Parallel.For(0, 200, i => log.Info($"строка {i}"));

        Assert.Equal(200, File.ReadAllLines(path).Length);
    }
}
