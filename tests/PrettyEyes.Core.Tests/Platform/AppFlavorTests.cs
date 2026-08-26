using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Stats;
using Xunit;

namespace PrettyEyes.Core.Tests.Platform;

public class AppFlavorTests
{
    [Theory]
    [InlineData("check")]
    [InlineData("Check")]
    [InlineData("  CHECK  ")]
    public void The_marker_is_read_the_way_a_build_script_writes_it(string marker)
    {
        Assert.Equal(AppFlavor.Check, AppFlavor.From(marker));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("release")]
    [InlineData("что угодно ещё")]
    public void Anything_that_is_not_the_marker_is_the_real_thing(string? marker)
    {
        // A build with a broken flag has to come out as the release build, not
        // as something in between with half an identity.
        Assert.Equal(AppFlavor.Release, AppFlavor.From(marker));
    }

    [Fact]
    public void The_two_builds_share_no_name_at_all()
    {
        // One shared name is one way for the check build to take over the real
        // one: its settings, its autostart entry, its place in the tray.
        var release = AppFlavor.Release;
        var check = AppFlavor.Check;

        Assert.NotEqual(release.DisplayName, check.DisplayName);
        Assert.NotEqual(release.DataFolder, check.DataFolder);
        Assert.NotEqual(release.MutexName, check.MutexName);
        Assert.NotEqual(release.AppUserModelId, check.AppUserModelId);
        Assert.NotEqual(release.AutostartValueName, check.AutostartValueName);
        Assert.NotEqual(release.IconAsset, check.IconAsset);
        Assert.NotEqual(release.LogoAsset, check.LogoAsset);
    }

    [Fact]
    public void Only_the_real_build_is_allowed_to_update_itself()
    {
        Assert.True(AppFlavor.Release.UpdatesAllowed);
        Assert.False(AppFlavor.Check.UpdatesAllowed);
    }

    [Fact]
    public void The_real_build_keeps_the_names_that_are_already_on_disk()
    {
        // Renaming any of these would orphan the settings, the log and the
        // autostart entry of everyone who already has the app.
        Assert.Equal("prettyeyes", AppFlavor.Release.DataFolder);
        Assert.Equal("PrettyEyesSingleInstance", AppFlavor.Release.MutexName);
        Assert.Equal("prettyeyes.app", AppFlavor.Release.AppUserModelId);
        Assert.Equal("prettyeyes", AppFlavor.Release.AutostartValueName);
    }

    [Fact]
    public void The_check_build_writes_nowhere_near_the_real_one()
    {
        // EndsWith rather than Contains on purpose: "prettyeyes" is a substring
        // of "prettyeyes-check", so a swapped pair would pass Contains.
        Assert.EndsWith(@"\prettyeyes-check\log.txt", Log.PathFor(AppFlavor.Check));
        Assert.EndsWith(@"\prettyeyes\log.txt", Log.PathFor(AppFlavor.Release));
        Assert.EndsWith(@"\prettyeyes-check\settings.json", JsonSettingsStore.PathFor(AppFlavor.Check));
        Assert.EndsWith(@"\prettyeyes\settings.json", JsonSettingsStore.PathFor(AppFlavor.Release));
        Assert.EndsWith(@"\prettyeyes-check\stats.json", JsonStatsStore.PathFor(AppFlavor.Check));
        Assert.EndsWith(@"\prettyeyes\stats.json", JsonStatsStore.PathFor(AppFlavor.Release));
    }

    [Fact]
    public void Under_the_test_host_there_is_no_marker_and_that_means_release()
    {
        // Entry assembly in a test run is testhost, which carries no metadata
        // of ours. Written down so a future change of test runner fails here
        // with a readable name instead of somewhere in a path assertion.
        Assert.Equal(AppFlavor.Release, AppFlavor.Current);
    }
}
