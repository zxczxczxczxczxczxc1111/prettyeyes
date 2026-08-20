using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// Stamps a glyph. A click places one at a size that suits the selection, a
/// drag sets the size by hand. Always square: a stretched emoji reads as a bug.
/// </summary>
public sealed class EmojiTool : ITool
{
    /// <summary>Below this a glyph is unrecognisable, above it it dominates.</summary>
    public const int MinSize = 24;
    public const int MaxSize = 96;

    /// <summary>A sixth of the shorter side, which lands around 64 on a normal region.</summary>
    private const int SelectionFraction = 6;

    private readonly SKImage _glyph;
    private readonly CaptureRect _selection;

    private int _x;
    private int _y;

    public EmojiTool(SKImage glyph, CaptureRect selection)
    {
        _glyph = glyph;
        _selection = selection;
    }

    /// <summary>
    /// What a plain click produces. Public because the size is a rule, not a
    /// detail: it has to agree with the minimum a drag can make.
    /// </summary>
    public static int DefaultSize(CaptureRect selection)
    {
        var shorter = Math.Min(selection.Width, selection.Height);

        return Math.Clamp(shorter / SelectionFraction, MinSize, MaxSize);
    }

    public void Begin(int x, int y)
    {
        _x = x;
        _y = y;
    }

    public IAnnotation? Preview(int x, int y) => Build(x, y);

    public IAnnotation? End(int x, int y) => Build(x, y);

    private IAnnotation Build(int x, int y)
    {
        var dragged = Math.Max(Math.Abs(x - _x), Math.Abs(y - _y));

        // A press that never moved is a click, and a click means the default.
        var size = dragged < MinSize ? DefaultSize(_selection) : Math.Min(dragged, MaxSize);

        // Grows away from where the press started, or is centred on it when it
        // was a click: a glyph appearing below and to the right of the cursor
        // would never land where it was aimed.
        var left = dragged < MinSize ? _x - (size / 2) : Math.Min(_x, x);
        var top = dragged < MinSize ? _y - (size / 2) : Math.Min(_y, y);

        return new EmojiAnnotation(new CaptureRect(left, top, size, size), _glyph);
    }
}
