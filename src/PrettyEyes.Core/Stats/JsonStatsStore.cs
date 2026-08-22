using System.Text.Json;

namespace PrettyEyes.Core.Stats;

/// <summary>
/// Counters in a small JSON file next to the settings, and never inside them.
///
/// Anything unreadable is treated as "no counts yet". That is the whole point
/// of keeping this apart: a corrupted counter must cost the user a number on a
/// screen, not their hotkeys and their toolbar.
/// </summary>
public sealed class JsonStatsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonStatsStore(string path) => _path = path;

    /// <summary>%APPDATA%\prettyeyes\stats.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "prettyeyes",
        "stats.json");

    public ShotStats Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return ShotStats.Empty;
            }

            return JsonSerializer.Deserialize<ShotStats>(File.ReadAllText(_path), Options)
                ?? ShotStats.Empty;
        }
        catch (JsonException)
        {
            return ShotStats.Empty;
        }
        catch (IOException)
        {
            return ShotStats.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return ShotStats.Empty;
        }
    }

    /// <summary>
    /// Returns whether it landed. Nobody is going to be told that a counter
    /// failed to save, but swallowing it silently inside is worse than letting
    /// the caller decide.
    /// </summary>
    public bool Save(ShotStats stats)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(stats, Options));

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
