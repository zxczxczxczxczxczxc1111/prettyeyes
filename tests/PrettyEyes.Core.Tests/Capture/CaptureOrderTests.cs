using PrettyEyes.Core.Capture;
using PrettyEyes.Core.Settings;

namespace PrettyEyes.Core.Tests.Capture;

public class CaptureOrderTests
{
    private static readonly string[] Chain =
    [
        CaptureOrder.Duplication,
        CaptureOrder.WindowsGraphicsCapture,
        CaptureOrder.Gdi,
    ];

    [Fact]
    public void Auto_leaves_the_chain_alone()
    {
        Assert.Equal(Chain, CaptureOrder.Of(CaptureSource.Auto, Chain));
    }

    [Fact]
    public void A_chosen_engine_goes_first()
    {
        Assert.Equal(
            [CaptureOrder.WindowsGraphicsCapture, CaptureOrder.Duplication, CaptureOrder.Gdi],
            CaptureOrder.Of(CaptureSource.WindowsGraphicsCapture, Chain));
    }

    /// <summary>
    /// The ones that were not chosen keep their order under the winner. They
    /// are not decoration: a machine where the chosen engine refuses a monitor
    /// still has to produce a screenshot, and the rest of the chain is what
    /// produces it.
    /// </summary>
    [Fact]
    public void The_others_stay_behind_it_in_their_own_order()
    {
        Assert.Equal(
            [CaptureOrder.Gdi, CaptureOrder.Duplication, CaptureOrder.WindowsGraphicsCapture],
            CaptureOrder.Of(CaptureSource.Gdi, Chain));
    }

    /// <summary>
    /// An engine the build does not have cannot be promoted, and saying so by
    /// dropping a painter would leave the machine with fewer ways to take a
    /// screenshot than it started with.
    /// </summary>
    [Fact]
    public void Choosing_an_engine_that_is_not_there_changes_nothing()
    {
        string[] withoutWgc = [CaptureOrder.Duplication, CaptureOrder.Gdi];

        Assert.Equal(
            withoutWgc,
            CaptureOrder.Of(CaptureSource.WindowsGraphicsCapture, withoutWgc));
    }
}
