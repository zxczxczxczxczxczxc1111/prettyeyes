using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class DefaultToolTests
{
    [Fact]
    public void The_default_applies_when_the_toolbar_appears()
    {
        Assert.Equal(ToolKind.Arrow, DefaultTool.Apply(ToolKind.Arrow, toolbarWasShown: false));
    }

    [Fact]
    public void Settling_the_selection_again_turns_nothing_on()
    {
        // OnSelectionSettled fires on every finished drag and on the double
        // click. Without the guard, dragging the frame by a grip would switch
        // the tool back on after the user turned it off.
        Assert.Null(DefaultTool.Apply(ToolKind.Arrow, toolbarWasShown: true));
    }

    [Fact]
    public void Nothing_chosen_stays_nothing_chosen()
    {
        Assert.Null(DefaultTool.Apply(null, toolbarWasShown: false));
    }
}
