using System.Text.Json;
using System.Text.RegularExpressions;

namespace PrettyEyes.Core.Updates;

/// <summary>
/// What a GitHub release says about itself, reduced to what an update needs.
///
/// The parsing lives in Core so it can be tested against a saved response
/// rather than against the network.
/// </summary>
public sealed partial record ReleaseInfo(ReleaseVersion Version, string AssetName, string Url, long Size, string? Sha256)
{
    /// <summary>The one asset an update is allowed to run.</summary>
    public static string AssetFor(ReleaseVersion version) => $"prettyeyes-setup-{version}.exe";

    /// <summary>
    /// Reads the release. Anything unexpected returns null rather than throwing:
    /// a broken answer from the network is a normal Tuesday, and the update
    /// check has to survive it quietly.
    /// </summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tag)
                || !ReleaseVersion.TryParse(tag.GetString(), out var version))
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var wanted = AssetFor(version);

            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), wanted, StringComparison.OrdinalIgnoreCase)
                    && asset.TryGetProperty("browser_download_url", out var url))
                {
                    var size = asset.TryGetProperty("size", out var length) ? length.GetInt64() : 0;
                    var body = root.TryGetProperty("body", out var text) ? text.GetString() : null;

                    return new ReleaseInfo(version, wanted, url.GetString() ?? string.Empty, size, Hash(body));
                }
            }

            // A release without the installer under the expected name is not an
            // update: whatever else is attached, it is not ours to run.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Pulls "sha256: abc..." out of the release notes.</summary>
    private static string? Hash(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var match = HashPattern().Match(body);

        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    [GeneratedRegex(@"sha256:\s*([0-9a-fA-F]{64})", RegexOptions.IgnoreCase)]
    private static partial Regex HashPattern();
}
