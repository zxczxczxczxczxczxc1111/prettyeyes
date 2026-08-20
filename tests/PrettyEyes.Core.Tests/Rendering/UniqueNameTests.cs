using PrettyEyes.Core.Rendering;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class UniqueNameTests
{
    [Fact]
    public void A_free_name_is_used_as_it_is()
    {
        Assert.Equal("shot.png", UniqueName.For("shot.png", _ => false));
    }

    [Fact]
    public void A_taken_name_gets_a_suffix()
    {
        Assert.Equal("shot-2.png", UniqueName.For("shot.png", name => name == "shot.png"));
    }

    [Fact]
    public void The_suffix_keeps_counting_while_names_are_taken()
    {
        var taken = new HashSet<string> { "shot.png", "shot-2.png", "shot-3.png" };

        Assert.Equal("shot-4.png", UniqueName.For("shot.png", taken.Contains));
    }

    [Fact]
    public void The_extension_stays_where_it_belongs()
    {
        var name = UniqueName.For("снимок-2026-08-20.png", n => n == "снимок-2026-08-20.png");

        Assert.Equal("снимок-2026-08-20-2.png", name);
    }

    [Fact]
    public void A_folder_full_of_the_same_name_gives_up_rather_than_spinning()
    {
        Assert.Throws<IOException>(() => UniqueName.For("shot.png", _ => true));
    }
}
