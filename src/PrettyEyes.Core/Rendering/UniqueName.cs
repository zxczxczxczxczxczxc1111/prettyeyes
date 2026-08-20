namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Finds a file name nobody has taken. Lives in Core rather than next to the
/// file writing so it can be tested without a disk.
/// </summary>
public static class UniqueName
{
    /// <summary>
    /// Enough attempts to cover a second's worth of screenshots. Past that
    /// something else is wrong, and looping forever would hide it.
    /// </summary>
    private const int Attempts = 1000;

    public static string For(string wanted, Func<string, bool> taken)
    {
        if (!taken(wanted))
        {
            return wanted;
        }

        var stem = Path.GetFileNameWithoutExtension(wanted);
        var extension = Path.GetExtension(wanted);

        for (var index = 2; index <= Attempts; index++)
        {
            var candidate = $"{stem}-{index}{extension}";

            if (!taken(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Не удалось подобрать свободное имя для {wanted}.");
    }
}
