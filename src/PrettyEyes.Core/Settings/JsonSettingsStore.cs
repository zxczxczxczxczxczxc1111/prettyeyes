using System.Text.Json;
using PrettyEyes.Core.Platform;

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

            var stored = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options);

            if (stored is null)
            {
                return AppSettings.Default;
            }

            return Normalize(stored);
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

    /// <summary>
    /// The one place where a file from an older version is brought up to date.
    /// Every field added by a later version needs a line here: JSON leaves an
    /// unknown property at its default, and for a reference type that default
    /// is null in a property declared as never null.
    /// </summary>
    private static AppSettings Normalize(AppSettings stored) => stored with
    {
        Hotkey = stored.Hotkey ?? HotkeyDefinition.Default,
        FullScreenHotkey = stored.FullScreenHotkey ?? HotkeyDefinition.DefaultFullScreen,
        SchemaVersion = AppSettings.CurrentSchema,
    };

    /// <summary>
    /// Written through a temporary file and moved into place: a half-written
    /// settings file reads as no settings at all, and losing every hotkey
    /// because the power went out mid-write is not a trade worth making.
    /// </summary>
    public bool Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        var temporary = _path + ".tmp";

        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, _path, overwrite: true);

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Reported by returning false: a storage class in Core has no
            // business writing into the user's log file, and the tests would
            // write there too.
            Discard(temporary);

            return false;
        }
    }

    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover .tmp is harmless; the next save overwrites it.
        }
    }
}
