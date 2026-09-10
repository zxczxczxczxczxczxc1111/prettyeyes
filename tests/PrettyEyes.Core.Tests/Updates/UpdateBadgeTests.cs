using PrettyEyes.Core.Updates;
using Xunit;

namespace PrettyEyes.Core.Tests.Updates;

/// <summary>
/// When the tray icon wears its mark, and what it says when asked.
///
/// The rule is not "when a check succeeded" but "when there is something the
/// person has not installed yet": that outlives the window being closed, the
/// notification being missed, and the check that found it happening twenty
/// hours ago.
/// </summary>
public class UpdateBadgeTests
{
    private static readonly ReleaseVersion Newer = new(1, 4, 5);

    [Fact]
    public void Nothing_has_happened_yet_so_there_is_no_mark()
    {
        Assert.False(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Idle)));
    }

    [Fact]
    public void Checking_is_not_news()
    {
        Assert.False(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Checking)));
    }

    [Fact]
    public void Up_to_date_clears_the_mark()
    {
        Assert.False(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.UpToDate, new ReleaseVersion(1, 4, 4))));
    }

    [Fact]
    public void A_newer_release_puts_the_mark_on()
    {
        Assert.True(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Available, Newer)));
    }

    [Fact]
    public void The_mark_stays_while_the_installer_downloads()
    {
        Assert.True(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Downloading, Newer, 0.4)));
    }

    [Fact]
    public void The_mark_stays_while_the_installer_runs()
    {
        Assert.True(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Installing, Newer)));
    }

    /// <summary>
    /// A download that broke halfway leaves the release exactly where it was.
    /// The settings window keeps its install button up in this case for the
    /// same reason, and a mark that vanished on the one failure worth retrying
    /// would be the wrong half of the pair.
    /// </summary>
    [Fact]
    public void A_failed_download_keeps_the_mark_because_the_release_is_still_there()
    {
        Assert.True(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Failed, Newer)));
    }

    /// <summary>
    /// A failure with no version behind it is a check that did not complete -
    /// no network, GitHub down. Nothing was found, so there is nothing to mark.
    /// </summary>
    [Fact]
    public void A_failed_check_leaves_the_icon_alone()
    {
        Assert.False(UpdateBadge.ShouldShow(new UpdateState(UpdateStage.Failed)));
    }

    [Fact]
    public void Without_an_update_the_tooltip_is_just_the_name()
    {
        Assert.Equal("prettyeyes", UpdateBadge.Tooltip("prettyeyes", new UpdateState(UpdateStage.UpToDate)));
    }

    [Fact]
    public void With_an_update_the_tooltip_names_the_version()
    {
        Assert.Equal(
            "prettyeyes: доступна 1.4.5",
            UpdateBadge.Tooltip("prettyeyes", new UpdateState(UpdateStage.Available, Newer)));
    }

    /// <summary>
    /// Shell_NotifyIcon truncates a tip at 128 characters, and a truncated one
    /// is not a smaller problem than a missing one - it is the same string with
    /// the version cut off the end.
    /// </summary>
    [Fact]
    public void A_long_name_does_not_push_the_version_out_of_the_tip()
    {
        var tip = UpdateBadge.Tooltip(new string('и', 200), new UpdateState(UpdateStage.Available, Newer));

        Assert.True(tip.Length <= 127, $"tip is {tip.Length} characters");
        Assert.EndsWith("доступна 1.4.5", tip);
    }
}
