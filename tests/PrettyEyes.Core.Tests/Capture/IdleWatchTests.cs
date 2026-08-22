using PrettyEyes.Core.Capture;
using Xunit;

namespace PrettyEyes.Core.Tests.Capture;

public class IdleWatchTests
{
    private static readonly DateTime Noon = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Local);

    private static IdleWatch Watch() => new(TimeSpan.FromMinutes(3));

    [Fact]
    public void Nothing_is_due_before_anything_has_been_used()
    {
        // Freshly started, the engine holds nothing, and releasing nothing is
        // a wasted wake-up at best.
        Assert.False(Watch().Due(Noon.AddHours(5)));
    }

    [Fact]
    public void Nothing_is_due_while_the_idle_spell_is_still_short()
    {
        var watch = Watch();
        watch.Touch(Noon);

        Assert.False(watch.Due(Noon.AddMinutes(2)));
    }

    [Fact]
    public void It_is_due_once_the_spell_is_long_enough()
    {
        var watch = Watch();
        watch.Touch(Noon);

        Assert.True(watch.Due(Noon.AddMinutes(3)));
    }

    [Fact]
    public void It_is_due_exactly_once_per_idle_spell()
    {
        // The caller releases when told. Telling it twice would have it
        // release something that is no longer there.
        var watch = Watch();
        watch.Touch(Noon);

        Assert.True(watch.Due(Noon.AddMinutes(5)));
        Assert.False(watch.Due(Noon.AddMinutes(9)));
    }

    [Fact]
    public void Using_it_again_starts_a_new_spell()
    {
        var watch = Watch();
        watch.Touch(Noon);
        watch.Due(Noon.AddMinutes(5));

        watch.Touch(Noon.AddMinutes(6));

        Assert.False(watch.Due(Noon.AddMinutes(8)));
        Assert.True(watch.Due(Noon.AddMinutes(9)));
    }
}
