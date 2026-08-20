using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Tests.Geometry;

public class PanelPlacementTests
{
    // A 2560x1440 monitor with a 48 pixel taskbar, the panel the toolbar is.
    private const double Gap = 8;
    private const double Panel = 44;
    private const double Screen = 1440;
    private const double Taskbar = 1392;

    [Fact]
    public void SitsUnderTheSelectionWhenThereIsRoom()
    {
        var y = PanelPlacement.Vertical(200, 400, Panel, Gap, 0, Taskbar);

        Assert.Equal(408, y);
    }

    [Fact]
    public void FlipsAboveTheSelectionWhenTheBottomIsBusy()
    {
        var y = PanelPlacement.Vertical(1000, 1380, Panel, Gap, 0, Taskbar);

        Assert.Equal(1000 - Gap - Panel, y);
    }

    [Fact]
    public void WholeMonitorSelectionKeepsThePanelAboveTheTaskbar()
    {
        var y = PanelPlacement.Vertical(0, Screen, Panel, Gap, 0, Taskbar);

        Assert.True(y + Panel <= Taskbar, $"panel ends at {y + Panel}, taskbar starts at {Taskbar}");
    }

    [Fact]
    public void WithoutATaskbarTheWholeMonitorSelectionUsesTheBottom()
    {
        var y = PanelPlacement.Vertical(0, Screen, Panel, Gap, 0, Screen);

        Assert.Equal(Screen - Gap - Panel, y);
    }

    [Fact]
    public void NeverGoesAboveTheTopLimit()
    {
        // A taskbar at the top, and a selection taller than what is left.
        var y = PanelPlacement.Vertical(0, Screen, Panel, Gap, 48, Screen);

        Assert.True(y >= 48);
    }

    [Fact]
    public void APanelTallerThanTheScreenStillStartsInsideIt()
    {
        var y = PanelPlacement.Vertical(0, Screen, 2000, Gap, 0, Taskbar);

        Assert.Equal(0, y);
    }
}
