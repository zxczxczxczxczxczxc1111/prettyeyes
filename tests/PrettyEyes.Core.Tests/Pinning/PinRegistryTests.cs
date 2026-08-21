using PrettyEyes.Core.Pinning;
using Xunit;

namespace PrettyEyes.Core.Tests.Pinning;

/// <summary>
/// Who is currently pinned. Split off from the window that shows it so this
/// half can be tested at all: the test project references Core and nothing
/// else, and a registry living in the app would be a registry no test can see.
/// </summary>
public class PinRegistryTests
{
    private sealed class Pin : IPinned
    {
        public bool HasOwnAnnotations { get; set; }
    }

    [Fact]
    public void Closing_one_leaves_the_others_alive()
    {
        var registry = new PinRegistry();
        var first = new Pin();
        var second = new Pin();
        var third = new Pin();

        registry.Add(first);
        registry.Add(second);
        registry.Add(third);
        registry.Remove(second);

        Assert.Equal([first, third], registry.Pins);
    }

    [Fact]
    public void Nobody_has_drawn_anything_yet()
    {
        var registry = new PinRegistry();
        registry.Add(new Pin());
        registry.Add(new Pin());

        // The question task 21 asks before an update restarts the app.
        Assert.False(registry.AnyWithAnnotations);
    }

    [Fact]
    public void One_pin_with_its_own_drawing_is_enough_to_say_yes()
    {
        var registry = new PinRegistry();
        registry.Add(new Pin());
        registry.Add(new Pin { HasOwnAnnotations = true });

        Assert.True(registry.AnyWithAnnotations);
    }

    [Fact]
    public void A_closed_pin_stops_counting()
    {
        var registry = new PinRegistry();
        var drawn = new Pin { HasOwnAnnotations = true };

        registry.Add(drawn);
        registry.Remove(drawn);

        Assert.False(registry.AnyWithAnnotations);
        Assert.Empty(registry.Pins);
    }

    [Fact]
    public void Removing_something_that_was_never_added_changes_nothing()
    {
        var registry = new PinRegistry();
        var pin = new Pin();

        registry.Add(pin);

        Assert.False(registry.Remove(new Pin()));
        Assert.Equal([pin], registry.Pins);
    }

    [Fact]
    public void The_list_handed_out_does_not_move_under_the_caller()
    {
        // Closing every pin means walking the list while each close removes
        // itself from it. A live view would throw halfway through.
        var registry = new PinRegistry();
        registry.Add(new Pin());
        registry.Add(new Pin());

        var snapshot = registry.Pins;
        registry.Remove(snapshot[0]);

        Assert.Equal(2, snapshot.Count);
        Assert.Single(registry.Pins);
    }
}
