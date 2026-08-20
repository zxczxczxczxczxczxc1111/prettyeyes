using System.Text.Json;
using PrettyEyes.Core.Updates;
using Xunit;

namespace PrettyEyes.Core.Tests.Updates;

public class ReleaseInfoTests
{
    private const string Hash = "9f2eaa77c2e10bd9d6d0c1a5a4f5e70a3b0c9d81f2a3b4c5d6e7f8091a2b3c4d";

    /// <summary>
    /// Built the way GitHub builds it, with the body properly escaped: a
    /// release note spans several lines, and pasting one in raw produces JSON
    /// that no parser accepts.
    /// </summary>
    private static string Release(string tag, string asset, string body = "") => $$"""
        {
          "tag_name": "{{tag}}",
          "body": {{JsonSerializer.Serialize(body)}},
          "assets": [
            {
              "name": "{{asset}}",
              "size": 37495568,
              "browser_download_url": "https://github.com/owner/repo/releases/download/{{tag}}/{{asset}}"
            }
          ]
        }
        """;

    [Fact]
    public void A_normal_release_gives_the_version_asset_and_link()
    {
        var info = ReleaseInfo.Parse(Release("v1.1.0", "prettyeyes-setup-1.1.0.exe"));

        Assert.NotNull(info);
        Assert.Equal(new ReleaseVersion(1, 1, 0), info!.Version);
        Assert.Equal("prettyeyes-setup-1.1.0.exe", info.AssetName);
        Assert.Equal(37495568, info.Size);
        Assert.StartsWith("https://github.com/", info.Url);
    }

    [Fact]
    public void The_hash_is_taken_from_the_release_notes()
    {
        var body = string.Join(
            Environment.NewLine,
            "Что нового",
            string.Empty,
            "Лупа и эмодзи.",
            string.Empty,
            $"sha256: {Hash}");

        var info = ReleaseInfo.Parse(Release("v1.1.0", "prettyeyes-setup-1.1.0.exe", body));

        Assert.Equal(Hash, info!.Sha256);
    }

    [Fact]
    public void Without_a_hash_the_release_still_reads_but_says_so()
    {
        var info = ReleaseInfo.Parse(Release("v1.1.0", "prettyeyes-setup-1.1.0.exe"));

        Assert.Null(info!.Sha256);
    }

    [Fact]
    public void An_asset_under_another_name_is_not_ours_to_run()
    {
        // Whatever else is attached to the release, only the installer named
        // after the version is allowed anywhere near a Process.Start.
        Assert.Null(ReleaseInfo.Parse(Release("v1.1.0", "totally-not-a-virus.exe")));
    }

    [Fact]
    public void An_asset_belonging_to_another_version_is_refused()
    {
        Assert.Null(ReleaseInfo.Parse(Release("v1.1.0", "prettyeyes-setup-1.0.9.exe")));
    }

    [Theory]
    [InlineData("{ \"tag_name\": \"latest\", \"assets\": [] }")]
    [InlineData("{ \"assets\": [] }")]
    [InlineData("{ \"tag_name\": \"v1.1.0\" }")]
    [InlineData("не json вовсе")]
    public void A_broken_answer_is_no_update_rather_than_an_exception(string json)
    {
        Assert.Null(ReleaseInfo.Parse(json));
    }
}
