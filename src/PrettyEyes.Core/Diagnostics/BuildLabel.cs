using System.Reflection;

namespace PrettyEyes.Core.Diagnostics;

/// <summary>
/// How a build names itself in the log and in the settings window.
///
/// A version number alone is not enough to tell two builds apart: 1.3.0 was
/// built twice, once for a manual check and once for the release, and the
/// update never offered the second one because both call themselves 1.3.0.
/// The commit is what separates them, so it goes wherever the version goes.
/// </summary>
public static class BuildLabel
{
    /// <summary>Enough of a commit to recognise it, short enough to read.</summary>
    private const int CommitLength = 7;

    private const string Unknown = "неизвестно";

    /// <summary>What this running build calls itself, commit included.</summary>
    public static string Current { get; } = From(
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);

    /// <summary>
    /// Turns "1.3.0+73dc5a0abfae..." into "1.3.0+73dc5a0". Anything without a
    /// commit is passed through: an older build, or one built outside git.
    /// </summary>
    public static string From(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return Unknown;
        }

        var text = informationalVersion.Trim();
        var plus = text.IndexOf('+');

        if (plus < 0)
        {
            return text;
        }

        var version = text[..plus];
        var commit = text[(plus + 1)..];

        return commit.Length > CommitLength
            ? $"{version}+{commit[..CommitLength]}"
            : text;
    }
}
