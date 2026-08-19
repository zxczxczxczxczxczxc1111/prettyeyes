using PrettyEyes.Core.Geometry;
using Xunit;

namespace PrettyEyes.Core.Tests.Geometry;

public class DesktopLayoutTests
{
    // Two 2560x1440 monitors side by side - the user's actual setup.
    private static DesktopLayout TwoMonitors() => new(new[]
    {
        new MonitorInfo("primary", new CaptureRect(0, 0, 2560, 1440), 1.0),
        new MonitorInfo("second", new CaptureRect(2560, 0, 2560, 1440), 1.0),
    });

    // Left monitor at negative X - a very common Windows layout.
    private static DesktopLayout NegativeOrigin() => new(new[]
    {
        new MonitorInfo("left", new CaptureRect(-1920, 0, 1920, 1080), 1.0),
        new MonitorInfo("primary", new CaptureRect(0, 0, 2560, 1440), 1.25),
    });

    [Fact]
    public void VirtualBounds_covers_all_monitors()
    {
        Assert.Equal(new CaptureRect(0, 0, 5120, 1440), TwoMonitors().VirtualBounds);
    }

    [Fact]
    public void VirtualBounds_handles_negative_origin()
    {
        Assert.Equal(new CaptureRect(-1920, 0, 4480, 1440), NegativeOrigin().VirtualBounds);
    }

    [Fact]
    public void MonitorAt_finds_monitor_under_point()
    {
        var layout = TwoMonitors();

        Assert.Equal("primary", layout.MonitorAt(100, 100)?.DeviceId);
        Assert.Equal("second", layout.MonitorAt(3000, 100)?.DeviceId);
    }

    [Fact]
    public void MonitorAt_on_the_seam_belongs_to_the_right_monitor()
    {
        // Half-open bounds: x=2560 is the first pixel of the second monitor.
        Assert.Equal("second", TwoMonitors().MonitorAt(2560, 100)?.DeviceId);
    }

    [Fact]
    public void MonitorAt_outside_any_monitor_returns_null()
    {
        Assert.Null(TwoMonitors().MonitorAt(9000, 9000));
    }

    [Fact]
    public void ToMonitorLocal_subtracts_monitor_origin()
    {
        var layout = TwoMonitors();

        var local = layout.ToMonitorLocal(layout.Monitors[1], new CaptureRect(2600, 50, 100, 80));

        Assert.Equal(new CaptureRect(40, 50, 100, 80), local);
    }

    [Fact]
    public void ToMonitorLocal_works_with_negative_origin()
    {
        var layout = NegativeOrigin();

        var local = layout.ToMonitorLocal(layout.Monitors[0], new CaptureRect(-1900, 10, 50, 50));

        Assert.Equal(new CaptureRect(20, 10, 50, 50), local);
    }

    [Fact]
    public void Empty_monitor_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new DesktopLayout(Array.Empty<MonitorInfo>()));
    }
}
