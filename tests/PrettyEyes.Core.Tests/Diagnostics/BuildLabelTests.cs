using PrettyEyes.Core.Diagnostics;
using Xunit;

namespace PrettyEyes.Core.Tests.Diagnostics;

public class BuildLabelTests
{
    [Fact]
    public void The_commit_is_shortened_to_something_a_person_can_read_aloud()
    {
        Assert.Equal(
            "1.3.0+73dc5a0",
            BuildLabel.From("1.3.0+73dc5a0abfaed71ca7494ed994cead04a2e98ef7"));
    }

    [Fact]
    public void Two_builds_of_one_version_do_not_look_alike()
    {
        // The whole reason this type exists: 1.3.0 shipped twice, and the
        // number alone said nothing about which binary was running.
        var released = BuildLabel.From("1.3.0+73dc5a0abfaed71ca7494ed994cead04a2e98ef7");
        var handed = BuildLabel.From("1.3.0+348a9e1362292fab70da0dc2791c398aac42b626");

        Assert.NotEqual(released, handed);
    }

    [Fact]
    public void A_version_without_a_commit_stays_as_it_is()
    {
        Assert.Equal("1.3.0", BuildLabel.From("1.3.0"));
    }

    [Fact]
    public void A_commit_shorter_than_the_cut_is_not_padded_or_trimmed()
    {
        Assert.Equal("1.3.0+abc", BuildLabel.From("1.3.0+abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_assembly_that_says_nothing_is_said_to_say_nothing(string? informational)
    {
        // Better a word than an empty place in the log, where a missing value
        // reads as a missing line.
        Assert.Equal("неизвестно", BuildLabel.From(informational));
    }
}
