using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Tools;

public class ToolVisibilityTests
{
    [Fact]
    public void Nothing_recorded_means_everything_is_shown()
    {
        var tools = new ToolVisibility();

        Assert.All(ToolVisibility.All, kind => Assert.True(tools.IsShown(kind)));
        Assert.Equal(ToolVisibility.All.Count, tools.ShownCount);
    }

    [Fact]
    public void A_tool_turned_off_stays_off()
    {
        var tools = new ToolVisibility();

        Assert.True(tools.TrySet(ToolKind.Emoji, false));
        Assert.False(tools.IsShown(ToolKind.Emoji));
        Assert.True(tools.IsShown(ToolKind.Blur));
    }

    [Fact]
    public void A_tool_can_be_turned_back_on()
    {
        var tools = new ToolVisibility();

        tools.TrySet(ToolKind.Line, false);

        Assert.True(tools.TrySet(ToolKind.Line, true));
        Assert.True(tools.IsShown(ToolKind.Line));
    }

    [Fact]
    public void The_last_tool_standing_cannot_be_turned_off()
    {
        var tools = new ToolVisibility();

        foreach (var kind in ToolVisibility.All.Where(kind => kind != ToolKind.Arrow))
        {
            Assert.True(tools.TrySet(kind, false));
        }

        Assert.False(tools.TrySet(ToolKind.Arrow, false));
        Assert.True(tools.IsShown(ToolKind.Arrow));
        Assert.Equal(1, tools.ShownCount);
    }

    [Fact]
    public void Turning_an_already_hidden_tool_off_again_is_allowed()
    {
        var tools = new ToolVisibility();

        foreach (var kind in ToolVisibility.All.Where(kind => kind != ToolKind.Arrow))
        {
            tools.TrySet(kind, false);
        }

        // It changes nothing, and refusing it would make the checkbox fight
        // the state it is already in.
        Assert.True(tools.TrySet(ToolKind.Emoji, false));
    }

    [Fact]
    public void A_tool_the_stored_file_never_heard_of_is_shown()
    {
        // A file written by a version that had no emoji says nothing about it.
        var tools = new ToolVisibility(new Dictionary<ToolKind, bool> { [ToolKind.Blur] = false });

        Assert.False(tools.IsShown(ToolKind.Blur));
        Assert.True(tools.IsShown(ToolKind.Emoji));
    }

    [Fact]
    public void What_goes_to_the_settings_file_comes_back_the_same()
    {
        var tools = new ToolVisibility();
        tools.TrySet(ToolKind.Line, false);

        var restored = new ToolVisibility(tools.ToDictionary());

        Assert.False(restored.IsShown(ToolKind.Line));
        Assert.True(restored.IsShown(ToolKind.Arrow));
    }

    [Fact]
    public void The_dictionary_handed_out_is_a_copy()
    {
        var tools = new ToolVisibility();
        var snapshot = tools.ToDictionary();

        tools.TrySet(ToolKind.Blur, false);

        Assert.Empty(snapshot);
    }
}
