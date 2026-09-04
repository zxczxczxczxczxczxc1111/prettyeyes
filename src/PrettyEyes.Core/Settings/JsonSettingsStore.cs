using System.Text.Json;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Tools;
using PrettyEyes.Core.Rendering;

namespace PrettyEyes.Core.Settings;

/// <summary>
/// Settings in a JSON file. Anything unreadable falls back to defaults: a
/// broken file must not keep the app from starting, and the next Save fixes it.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    // Converters must be handed over here. Touch the collection after the first
    // serialize and STJ throws, because the options froze and told nobody.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new ToolKindKeyConverter() },
    };

    private readonly string _path;

    public JsonSettingsStore(string path) => _path = path;

    /// <summary>settings.json в папке той сборки, о которой спрашивают.</summary>
    public static string PathFor(AppFlavor flavor) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        flavor.DataFolder,
        "settings.json");

    /// <summary>Путь этой сборки.</summary>
    public static string DefaultPath => PathFor(AppFlavor.Current);

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

        // Schema 9 let the toolbar be cut down. Nothing recorded means nothing
        // was turned off, which is every tool shown.
        Tools = stored.Tools ?? [],

        // Schema 10 added the cursor. A missing enum reads as its first value,
        // which is the crosshair everyone had until now, so nothing to do.

        // Schema 14 let the capture engine be chosen by hand. A missing enum
        // reads as its first value, which is the automatic chain everyone had
        // until now, so there is nothing to fill in.

        // Schema 4 added per-tool styles; schema 13 replaced the three
        // thickness presets with a number of pixels and took the marker's
        // hidden multiplier away with it. A style older than that carries a
        // preset and no number, and only here is the tool known - which matters,
        // because the marker drew four times what its preset said.
        ToolStyles = stored.SchemaVersion >= 13
            ? stored.ToolStyles ?? []
            : Migrate(stored.ToolStyles),

        SchemaVersion = AppSettings.CurrentSchema,
    };

    /// <summary>How much wider the released build draws a highlighter.</summary>
    private const int MarkerFactor = 4;

    /// <summary>
    /// What schema 13 does to every stored tool style: a preset becomes the
    /// number of pixels it used to put on the screen, and a stored arrow starts
    /// drawing freehand.
    ///
    /// The arrow is here rather than in the defaults because a default cannot
    /// reach a tool the user has already touched - one change of colour and the
    /// style is in the file for good. Nobody picked the straight arrow on
    /// purpose either: the choice did not exist in any released build.
    /// </summary>
    private static Dictionary<ToolKind, ToolStyle> Migrate(
        Dictionary<ToolKind, ToolStyle>? stored)
    {
        var migrated = new Dictionary<ToolKind, ToolStyle>();

        if (stored is null)
        {
            return migrated;
        }

        foreach (var (kind, style) in stored)
        {
            if (style is null)
            {
                continue;
            }

            var drawn = PresetWidth(style.Size);

            if (kind == ToolKind.Marker)
            {
                drawn *= MarkerFactor;
            }

            migrated[kind] = kind == ToolKind.Arrow
                ? style.WithWidth(drawn) with { FreehandArrow = true }
                : style.WithWidth(drawn);
        }

        return migrated;
    }

    /// <summary>What each of the three old presets put on the screen.</summary>
    private static int PresetWidth(StrokeSize size) => size switch
    {
        StrokeSize.Small => 2,
        StrokeSize.Large => 5,
        _ => ToolStyle.DefaultWidth,
    };

    /// <summary>
    /// The thickness as the released build will read it.
    ///
    /// That build has no Width and draws by preset, multiplying the marker's by
    /// four. Written here rather than wherever a width is chosen, because only
    /// here is the tool known - and a preset computed without the tool is four
    /// times wrong for the highlighter, every single time. Doing it at the file
    /// boundary also means a style that arrived any other way, from another
    /// machine or from a hand-edited file, is put right before it is written.
    /// </summary>
    private static Dictionary<ToolKind, ToolStyle> WithLegacyPresets(
        IReadOnlyDictionary<ToolKind, ToolStyle> styles)
    {
        var mirrored = new Dictionary<ToolKind, ToolStyle>();

        foreach (var (kind, style) in styles)
        {
            var drawn = (int)style.StrokeWidth;
            var asPreset = kind == ToolKind.Marker ? drawn / MarkerFactor : drawn;

            mirrored[kind] = style with { Size = ToolStyle.NearestSize(asPreset) };
        }

        return mirrored;
    }

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

            var written = settings with
            {
                ToolStyles = WithLegacyPresets(settings.ToolStyles ?? []),
            };

            File.WriteAllText(temporary, JsonSerializer.Serialize(written, Options));
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
