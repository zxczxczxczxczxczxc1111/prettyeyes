using Avalonia.Platform;
using PrettyEyes.Core.Diagnostics;
using SkiaSharp;

namespace PrettyEyes.App.Services;

/// <summary>
/// The bundled glyphs, decoded when one is actually stamped.
///
/// Bundled rather than taken from the system font: colour glyph rendering
/// through Skia on Windows is not something to rely on, and a screenshot tool
/// whose emoji look different on every machine is a screenshot tool with a bug
/// report waiting.
///
/// Lazy rather than warmed at start-up. The files are 256 pixels square now,
/// so decoding all of them would cost 29 pictures of memory for a session that
/// usually stamps none; one glyph decodes in well under a millisecond, and by
/// the time it is asked for, the person has already clicked the tool. What the
/// picker shows is a different, smaller set of files - see EmojiPickerView.
/// </summary>
public sealed class EmojiAtlas : IDisposable
{
    /// <summary>
    /// In the order they are shown. Laughing first, then the rest of the
    /// faces by mood, then hands, then the things that are not faces at all.
    ///
    /// The codes are the Unicode ones wherever a glyph means the same thing as
    /// a standard emoji, because they end up in settings.json: a person whose
    /// chosen emoji is 1f480 must still have their skull after an update.
    /// Variants of one glyph get a suffix.
    /// </summary>
    private static readonly string[] Codes =
    [
        "1f602", "1f602-2", "1f601", "1f61d", "1f61b", "1f61c", "1f60d", "1f970",
        "1f60e", "1f97a", "1f622", "1f62d", "1f971", "1f635", "1f92e", "1f47f",
        "1f921", "1f921-2", "1f435", "1f648", "1f47d", "1f4a9", "1f44d", "1f44e",
        "1f64f", "1fa77", "1f480", "1f480-2", "1f480-3",
    ];

    private static readonly HashSet<string> Known = new(Codes, StringComparer.Ordinal);

    private readonly Dictionary<string, SKImage> _glyphs = [];
    private readonly object _gate = new();

    public static IReadOnlyList<string> All => Codes;

    /// <summary>
    /// Whether this code is one of ours at all. Asked before a tool is armed:
    /// a code left in the settings by an older set is not an error, it just
    /// means the person has to pick again.
    /// </summary>
    public static bool Has(string? code) => code is not null && Known.Contains(code);

    /// <summary>
    /// The picture for a code, decoded on first use and kept for the process.
    /// A glyph that fails to load is not worth a message: the tool behaves as
    /// though nothing was picked.
    /// </summary>
    public SKImage? Glyph(string code)
    {
        lock (_gate)
        {
            if (_glyphs.TryGetValue(code, out var cached))
            {
                return cached;
            }

            if (!Known.Contains(code))
            {
                return null;
            }

            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://PrettyEyes.App/Assets/Emoji/{code}.png"));
                var image = SKImage.FromEncodedData(stream);

                if (image is null)
                {
                    return null;
                }

                _glyphs[code] = image;

                return image;
            }
            catch (Exception error) when (error is FileNotFoundException or ArgumentException)
            {
                Log.Default.Error($"глиф {code} не загрузился", error);

                return null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var glyph in _glyphs.Values)
            {
                glyph.Dispose();
            }

            _glyphs.Clear();
        }
    }
}
