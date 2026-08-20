using Avalonia.Platform;
using PrettyEyes.Core.Diagnostics;
using SkiaSharp;

namespace PrettyEyes.App.Services;

/// <summary>
/// The bundled glyphs, decoded once and kept.
///
/// Twemoji, CC-BY 4.0. The set is bundled rather than taken from the system
/// font: colour glyph rendering through Skia on Windows is not something to
/// rely on, and a screenshot tool whose emoji look different on every machine
/// is a screenshot tool with a bug report waiting.
///
/// Decoding all forty costs a few milliseconds, but it happens while the screen
/// is frozen and the user is waiting, so it is done at start-up instead.
/// </summary>
public sealed class EmojiAtlas : IDisposable
{
    /// <summary>
    /// In the order they are shown. Faces first, then hands, then the marks
    /// people put on screenshots to say "look here" and "this is wrong".
    /// </summary>
    private static readonly string[] Codes =
    [
        "1f602", "1f923", "1f60d", "1f618", "1f60e", "1f914", "1f610", "1f644",
        "1f62d", "1f621", "1f631", "1f925", "1f973", "1f60f", "1f634", "1f92f",
        "1f480", "1f44d", "1f44e", "1f44c", "1f44f", "1f64f", "1f4aa", "1f440",
        "1f9e0", "2764", "1f494", "1f525", "2728", "1f4a5", "2705", "274c",
        "26a0", "2757", "2753", "1f4a1", "1f4cc", "1f680", "1f389", "1f4a9",
    ];

    private readonly Dictionary<string, SKImage> _glyphs = [];
    private readonly object _gate = new();

    private bool _loaded;

    public static IReadOnlyList<string> All => Codes;

    /// <summary>
    /// Decodes everything, in the background. Failing to load a glyph is not
    /// worth a message: the grid simply shows one fewer.
    /// </summary>
    public Task WarmAsync() => Task.Run(() =>
    {
        using var scope = Log.Default.Scope("emoji.warm");

        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            foreach (var code in Codes)
            {
                try
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://PrettyEyes.App/Assets/Emoji/{code}.png"));
                    var image = SKImage.FromEncodedData(stream);

                    if (image is not null)
                    {
                        _glyphs[code] = image;
                    }
                }
                catch (Exception error) when (error is FileNotFoundException or ArgumentException)
                {
                    Log.Default.Error($"глиф {code} не загрузился", error);
                }
            }

            _loaded = true;
        }
    });

    public SKImage? Glyph(string code)
    {
        lock (_gate)
        {
            return _glyphs.GetValueOrDefault(code);
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
            _loaded = false;
        }
    }
}
