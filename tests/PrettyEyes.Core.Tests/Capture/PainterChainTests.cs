using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using Xunit;

namespace PrettyEyes.Core.Tests.Capture;

public class PainterChainTests
{
    private static MonitorInfo Monitor(string id, int x = 0, int width = 100) =>
        new(id, new CaptureRect(x, 0, width, 100), 1.0);

    /// <summary>
    /// Paints nothing and remembers everything. The chain is a policy, and a
    /// policy is tested by what it asks of whom.
    /// </summary>
    private sealed class Fake(string name, Func<MonitorInfo, Exception?> answer) : IMonitorPainter
    {
        public string Name { get; } = name;

        public List<string> Asked { get; } = [];

        public int Disposals { get; private set; }

        public void Paint(MonitorInfo monitor, IntPtr destination, int stride)
        {
            Asked.Add(monitor.DeviceId);

            var refusal = answer(monitor);

            if (refusal is not null)
            {
                throw refusal;
            }
        }

        public void Dispose() => Disposals++;
    }

    private static Fake Willing(string name) => new(name, _ => null);

    private static Fake Refusing(string name) => new(name, _ => new NotSupportedException("not mine"));

    private static void Paint(PainterChain chain, MonitorInfo monitor) =>
        chain.Paint(monitor, IntPtr.Zero, 400);

    /// <summary>
    /// Into a temporary file, never into the real one: fake painters refusing
    /// fake monitors used to land in the user's own log and read like a broken
    /// installation.
    /// </summary>
    private static PainterChain Chain(IReadOnlyList<IMonitorPainter> painters) =>
        new(painters, new Log(Path.Combine(Path.GetTempPath(), "prettyeyes-tests", "chain.log")));

    [Fact]
    public void The_first_painter_that_can_do_it_paints()
    {
        var first = Willing("first");
        var second = Willing("second");
        using var chain = Chain([first, second]);

        Paint(chain, Monitor("one"));

        Assert.Equal(["one"], first.Asked);
        Assert.Empty(second.Asked);
        Assert.Equal("first", chain.Assignments["one"]);
    }

    [Fact]
    public void A_refusal_hands_the_monitor_to_the_next_painter()
    {
        var first = Refusing("first");
        var second = Willing("second");
        using var chain = Chain([first, second]);

        Paint(chain, Monitor("one"));

        Assert.Equal("second", chain.Assignments["one"]);
    }

    [Fact]
    public void A_painter_that_refused_a_monitor_is_never_asked_about_it_again()
    {
        // NotSupportedException means "this monitor is not mine": a rotated
        // output, a pixel format we do not read, an adapter that will not
        // duplicate. That answer does not change while the monitor does not.
        var first = Refusing("first");
        var second = Willing("second");
        using var chain = Chain([first, second]);

        Paint(chain, Monitor("one"));
        Paint(chain, Monitor("one"));
        Paint(chain, Monitor("one"));

        Assert.Single(first.Asked);
    }

    [Fact]
    public void A_painter_that_simply_failed_is_asked_again_next_time()
    {
        // Anything else is bad luck rather than a verdict: the duplication was
        // lost to a mode change, a game took the screen, a UAC prompt came and
        // went. Latching the application onto the older engine for that would
        // bring the yellow border back until the next restart.
        var moody = new Fake("moody", _ => new InvalidOperationException("no frame in time"));
        var spare = Willing("spare");
        using var chain = Chain([moody, spare]);

        Paint(chain, Monitor("one"));
        Paint(chain, Monitor("one"));

        Assert.Equal(["one", "one"], moody.Asked);
    }

    [Fact]
    public void Refusals_are_remembered_per_monitor_and_not_for_the_whole_desktop()
    {
        var picky = new Fake("picky", monitor =>
            monitor.DeviceId == "rotated" ? new NotSupportedException("not mine") : null);
        var spare = Willing("spare");
        using var chain = Chain([picky, spare]);

        Paint(chain, Monitor("rotated"));
        Paint(chain, Monitor("plain", 100));

        Assert.Equal("spare", chain.Assignments["rotated"]);
        Assert.Equal("picky", chain.Assignments["plain"]);
    }

    [Fact]
    public void A_monitor_that_changed_size_is_offered_to_the_refuser_again()
    {
        // A refusal is about this monitor as it is now. A new resolution can
        // mean a new pixel format, and the verdict was about the old one.
        var first = Refusing("first");
        var second = Willing("second");
        using var chain = Chain([first, second]);

        Paint(chain, Monitor("one"));
        Paint(chain, Monitor("one", width: 200));

        Assert.Equal(["one", "one"], first.Asked);
    }

    [Fact]
    public void A_monitor_nobody_can_paint_fails_the_capture_by_name()
    {
        var first = Refusing("first");
        var second = Refusing("second");
        using var chain = Chain([first, second]);

        var error = Assert.Throws<InvalidOperationException>(() => Paint(chain, Monitor("orphan")));

        Assert.Contains("orphan", error.Message);
    }

    [Fact]
    public void The_last_real_failure_is_the_reason_the_capture_gives()
    {
        var first = Refusing("first");
        var second = new Fake("second", _ => new InvalidOperationException("no frame in time"));
        using var chain = Chain([first, second]);

        var error = Assert.Throws<InvalidOperationException>(() => Paint(chain, Monitor("one")));

        Assert.Contains("no frame in time", error.ToString());
    }

    [Fact]
    public void A_monitor_that_went_away_leaves_the_list_of_who_paints_what()
    {
        // Unplugging a screen must not leave it in the line the log prints
        // about who paints what: that line is the one somebody reads when the
        // yellow border comes back, and a ghost monitor in it wastes the only
        // clue they have.
        var only = Willing("only");
        using var chain = Chain([only]);

        Paint(chain, Monitor("kept"));
        Paint(chain, Monitor("unplugged", 100));
        chain.KeepOnly(["kept"]);

        Assert.Equal(["kept"], chain.Assignments.Keys);
    }

    [Fact]
    public void Every_painter_is_disposed_once()
    {
        var first = Willing("first");
        var second = Willing("second");
        var chain = Chain([first, second]);

        chain.Dispose();
        chain.Dispose();

        Assert.Equal(1, first.Disposals);
        Assert.Equal(1, second.Disposals);
    }
}
