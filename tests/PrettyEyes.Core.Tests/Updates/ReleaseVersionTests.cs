using PrettyEyes.Core.Updates;
using Xunit;

namespace PrettyEyes.Core.Tests.Updates;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.0.1", 1, 0, 1)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("  2.0  ", 2, 0, 0)]
    public void A_tag_is_read_the_way_a_person_reads_it(string tag, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(tag, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("1.-2.3")]
    [InlineData("1.x.3")]
    public void Anything_that_is_not_a_version_is_refused(string tag)
    {
        Assert.False(ReleaseVersion.TryParse(tag, out _));
    }

    [Fact]
    public void Ten_is_newer_than_nine_rather_than_older()
    {
        // The trap of comparing versions as text.
        Assert.True(new ReleaseVersion(1, 0, 10).NewerThan(new ReleaseVersion(1, 0, 9)));
    }

    [Fact]
    public void The_same_version_is_not_newer_than_itself()
    {
        Assert.False(new ReleaseVersion(1, 1, 0).NewerThan(new ReleaseVersion(1, 1, 0)));
    }

    [Fact]
    public void An_older_version_never_counts_as_an_update()
    {
        Assert.False(new ReleaseVersion(1, 0, 0).NewerThan(new ReleaseVersion(1, 1, 0)));
        Assert.False(new ReleaseVersion(1, 9, 9).NewerThan(new ReleaseVersion(2, 0, 0)));
    }

    [Fact]
    public void It_prints_the_way_it_is_shown_to_people()
    {
        Assert.Equal("1.1.0", new ReleaseVersion(1, 1, 0).ToString());
    }
}
