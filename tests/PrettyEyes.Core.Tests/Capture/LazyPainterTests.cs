using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using Xunit;

namespace PrettyEyes.Core.Tests.Capture;

public class LazyPainterTests
{
    private static MonitorInfo Monitor(string id = "one") =>
        new(id, new CaptureRect(0, 0, 100, 100), 1.0);

    private sealed class Fake : IMonitorPainter
    {
        public string Name => "fake";

        public int Painted { get; private set; }

        public int Disposals { get; private set; }

        public void Paint(MonitorInfo monitor, IntPtr destination, int stride) => Painted++;

        public void Dispose() => Disposals++;
    }

    [Fact]
    public void Nothing_is_built_until_something_needs_painting()
    {
        var built = 0;
        using var painter = new LazyPainter("spare", () => { built++; return new Fake(); });

        Assert.Equal(0, built);
        Assert.False(painter.Built);
    }

    [Fact]
    public void The_name_is_known_before_anything_is_built()
    {
        // The chain writes the name into the log when a painter refuses a
        // monitor, and that can happen before this one is ever needed.
        using var painter = new LazyPainter("spare", () => new Fake());

        Assert.Equal("spare", painter.Name);
    }

    [Fact]
    public void The_engine_is_built_once_however_many_monitors_ask()
    {
        var built = 0;
        using var painter = new LazyPainter("spare", () => { built++; return new Fake(); });

        painter.Paint(Monitor(), IntPtr.Zero, 400);
        painter.Paint(Monitor("two"), IntPtr.Zero, 400);

        Assert.Equal(1, built);
    }

    [Fact]
    public void Painting_reaches_the_engine()
    {
        var real = new Fake();
        using var painter = new LazyPainter("spare", () => real);

        painter.Paint(Monitor(), IntPtr.Zero, 400);

        Assert.Equal(1, real.Painted);
    }

    [Fact]
    public void Disposing_an_engine_that_was_never_built_does_nothing()
    {
        var built = 0;
        var painter = new LazyPainter("spare", () => { built++; return new Fake(); });

        painter.Dispose();

        Assert.Equal(0, built);
    }

    [Fact]
    public void A_built_engine_is_disposed_once()
    {
        var real = new Fake();
        var painter = new LazyPainter("spare", () => real);

        painter.Paint(Monitor(), IntPtr.Zero, 400);
        painter.Dispose();
        painter.Dispose();

        Assert.Equal(1, real.Disposals);
    }

    [Fact]
    public void A_disposed_painter_refuses_to_build_anything_new()
    {
        var painter = new LazyPainter("spare", () => new Fake());
        painter.Dispose();

        Assert.Throws<ObjectDisposedException>(() => painter.Paint(Monitor(), IntPtr.Zero, 400));
    }
}
