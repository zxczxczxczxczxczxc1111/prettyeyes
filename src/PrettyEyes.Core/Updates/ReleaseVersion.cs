using System.Globalization;

namespace PrettyEyes.Core.Updates;

/// <summary>
/// A released version, as it appears in a tag: 1.0.1, sometimes with a v.
///
/// Deliberately not System.Version: that one treats a missing part as -1 and
/// compares 1.0 as older than 1.0.0, which is not how anybody reads a tag.
/// </summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = new ReleaseVersion(0, 0, 0);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().TrimStart('v', 'V');
        var parts = trimmed.Split('.');

        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        var numbers = new int[3];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new ReleaseVersion(numbers[0], numbers[1], numbers[2]);

        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var major = Major.CompareTo(other.Major);

        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);

        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public bool NewerThan(ReleaseVersion other) => CompareTo(other) > 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
