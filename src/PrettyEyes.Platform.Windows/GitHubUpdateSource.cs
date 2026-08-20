using System.Security.Cryptography;
using PrettyEyes.Core.Updates;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Releases from the project's own repository, and nothing else.
///
/// The rules are deliberately narrow: one address, https only, the asset named
/// after the version, the hash from the release notes. An update is the one
/// feature that runs an executable on somebody's machine, so everything it
/// touches is decided here rather than read out of the answer.
/// </summary>
public sealed class GitHubUpdateSource : IUpdateSource, IDisposable
{
    private const string Latest =
        "https://api.github.com/repos/zxczxczxczxczxczxc1111/prettyeyes/releases/latest";

    /// <summary>
    /// The same releases, for a human to read before they install anything.
    /// Newest first, so it needs no version in it and cannot go stale.
    /// </summary>
    public const string ReleasesPage =
        "https://github.com/zxczxczxczxczxczxc1111/prettyeyes/releases";

    /// <summary>GitHub refuses anonymous calls without one.</summary>
    private const string Agent = "prettyeyes-updater";

    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public GitHubUpdateSource()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", Agent);
        _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public async Task<ReleaseInfo?> LatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await _client.GetStringAsync(Latest, cancellationToken);

            return ReleaseInfo.Parse(json);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            // No network, no GitHub, no answer in ten seconds: all the same
            // thing to the caller, and none of them is worth an exception.
            return null;
        }
    }

    public async Task<string?> DownloadAsync(
        ReleaseInfo release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // Only ever the asset this release named, and only over https to
        // GitHub: a link out of the release body is somebody else's idea.
        if (!release.Url.StartsWith("https://github.com/", StringComparison.Ordinal))
        {
            return null;
        }

        // A fresh directory nobody else can guess: between checking the hash
        // and running the file, anything writable by this user could swap it.
        var directory = Path.Combine(Path.GetTempPath(), $"prettyeyes-update-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, release.AssetName);

        try
        {
            Directory.CreateDirectory(directory);

            using var response = await _client.GetAsync(
                release.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? release.Size;

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(path))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;

                // Whole percents only. A report per 80 KB chunk is five hundred
                // messages posted to the UI thread for one download, and the
                // line they redraw cannot show the difference anyway.
                var reported = -1;

                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    done += read;

                    if (total <= 0)
                    {
                        continue;
                    }

                    var percent = (int)(done * 100 / total);

                    if (percent != reported)
                    {
                        reported = percent;
                        progress?.Report(percent / 100.0);
                    }
                }
            }

            return Verify(path, release) ? path : Discard(directory);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return Discard(directory);
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Size and hash. A release without a published hash is refused outright:
    /// the whole point of running an installer unattended is knowing which
    /// installer it is.
    /// </summary>
    private static bool Verify(string path, ReleaseInfo release)
    {
        var file = new FileInfo(path);

        if (release.Size > 0 && file.Length != release.Size)
        {
            return false;
        }

        if (release.Sha256 is null)
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

        return hash == release.Sha256;
    }

    private static string? Discard(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover directory in the temporary folder is not worth caring
            // about; Windows clears it eventually.
        }

        return null;
    }
}
