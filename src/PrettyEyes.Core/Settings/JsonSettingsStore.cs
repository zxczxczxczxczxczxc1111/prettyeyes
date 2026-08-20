using System.Text.Json;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;

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

        // Schema 2 added the magnifier. A file older than that has the property
        // missing, and JSON leaves a missing bool false - which would silently
        // switch off a feature that ships switched on, for everyone updating.
        ShowMagnifier = stored.SchemaVersion >= 2 ? stored.ShowMagnifier : true,

        // Schema 3 added the pixel grid, on by default for the same reason.
        MagnifierGrid = stored.SchemaVersion >= 3 ? stored.MagnifierGrid : true,

        // Schema 4 added per-tool styles. Missing means every tool draws the
        // default, which is exactly what an empty dictionary says.
        ToolStyles = stored.ToolStyles ?? [],

        // Schema 5 added emoji. No glyph chosen yet is a valid state: the grid
        // opens on the first click instead of stamping something arbitrary.
        RecentEmoji = stored.RecentEmoji ?? [],

        // Schema 6 added the folder autosave, off by default: a screenshot tool
        // writing files into a folder nobody chose is a surprise.
        Save = stored.Save ?? SaveOptions.Default,

        // Schema 7 added the export frame, off by default: a screenshot that
        // silently grows a border is not the screenshot that was taken.
        Export = stored.Export ?? ExportStyle.None,

        // Schema 8 added the update check, on by default. A file older than
        // that has the property missing, and a missing bool reads as false -
        // which would leave every existing install checking for nothing.
        CheckUpdates = stored.SchemaVersion >= 8 ? stored.CheckUpdates : true,
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
