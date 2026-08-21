using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class ActiveToolTests
{
    [Fact]
    public void Clicking_the_same_tool_clears_the_selection()
    {
        Assert.Null(ToolSelection.Next(current: ToolKind.Arrow, clicked: ToolKind.Arrow));
    }

    [Fact]
    public void Clicking_another_tool_selects_it()
    {
        Assert.Equal(ToolKind.Line, ToolSelection.Next(current: ToolKind.Arrow, clicked: ToolKind.Line));
    }

    [Fact]
    public void Clicking_with_nothing_selected_turns_the_tool_on_first_try()
    {
        // The bug: the toolbar kept a stale "current" from the previous capture,
        // so the first click read as "turn the thing off" and ate itself.
        Assert.Equal(ToolKind.Arrow, ToolSelection.Next(current: null, clicked: ToolKind.Arrow));
    }
}
