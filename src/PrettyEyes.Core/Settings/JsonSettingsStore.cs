using System.Text.Json;

namespace PrettyEyes.Core.Settings;

/// <summary>
/// Settings in a JSON file. Anything unreadable falls back to defaults: a
/// broken file must not keep the app from starting, and the next Save fixes it.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonSettingsStore(string path) => _path = path;

    /// <summary>%APPDATA%\prettyeyes\settings.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "prettyeyes",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return AppSettings.Default;
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options)
                ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
    }
}
