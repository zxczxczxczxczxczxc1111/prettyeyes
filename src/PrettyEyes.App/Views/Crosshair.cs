using Avalonia;
using Avalonia.Input;
using PrettyEyes.Core.Settings;
using SkiaSharp;

namespace PrettyEyes.App.Views;

/// <summary>
/// The pointer over a frozen screen: a choice of shapes, each in a light and a
/// dark version, picked by what is underneath.
///
/// The system crosshair is a single black shape and disappears against anything
/// dark, which on a screenshot tool means half the screenshots people take.
/// Drawing the cursor on the canvas would fix the colour and cost a frame of
/// lag on the one thing nobody tolerates lag on, so instead every shape is
/// prepared twice and the caller swaps between them: the swap is a pointer
/// assignment, and the cursor stays drawn by the system.
///
/// Each shape carries a halo of the opposite colour, so the moment of the swap
/// is never a moment of invisibility, and a cursor sitting across an edge stays
/// readable on both sides of it.
/// </summary>
public static class Crosshair
{
    private const int Size = 32;

    /// <summary>
    /// The half-pixel keeps strokes on pixel boundaries: a one-pixel line
    /// centred on a whole coordinate lands half in each neighbour and comes out
    /// two grey pixels wide.
    /// </summary>
    private const float Centre = 16.5f;

    /// <summary>Shapes are drawn in a 16-unit box; the cursor is bigger.</summary>
    private const float Scale = 1.5f;

    private const float Middle = 8;

    /// <summary>
    /// Every shape, drawn in a 16 by 16 box around the middle. The same strings
    /// feed the cursor and the row of choices in the settings, so the two can
    /// never drift apart.
    /// </summary>
    private static readonly Dictionary<CursorStyle, string> Shapes = new()
    {
        [CursorStyle.Cross] = "M8,2 V14 M2,8 H14",
        [CursorStyle.Gap] = "M8,2 V6 M8,10 V14 M2,8 H6 M10,8 H14",
        [CursorStyle.Dot] = "M8,5 A3,3 0 1 0 8.01,5",
        [CursorStyle.Scope] = "M8,4 A4,4 0 1 0 8.01,4 M8,1 V2.5 M8,13.5 V15 M1,8 H2.5 M13.5,8 H15",
        [CursorStyle.Arrow] = "M5.5,2.5 V13 L8,10.5 L9.8,14 L11.4,13.2 L9.7,10 H13 Z",
    };

    /// <summary>
    /// Shapes with a curve in them. A hairline is drawn without smoothing so it
    /// stays exactly one pixel wide, which is right for straight lines and
    /// turns a small circle into a polygon.
    /// </summary>
    private static readonly HashSet<CursorStyle> Curved = [CursorStyle.Dot, CursorStyle.Scope];

    private static readonly Dictionary<CursorStyle, Cursor> LightCursors = [];
    private static readonly Dictionary<CursorStyle, Cursor> DarkCursors = [];

    private static readonly Cursor SystemArrow = new(StandardCursorType.Arrow);

    public static IReadOnlyList<CursorStyle> All { get; } =
        [CursorStyle.Cross, CursorStyle.Gap, CursorStyle.Dot, CursorStyle.Scope, CursorStyle.Arrow];

    /// <summary>The outline for the settings row, in its own 16 by 16 box.</summary>
    public static string Icon(CursorStyle style) => Shapes[style];

    /// <summary>
    /// The cursor for this shape against this colour: light ink on something
    /// dark, dark ink on something light. Null means the pointer is off the
    /// captured frame and there is nothing to judge against.
    /// </summary>
    public static Cursor For(CursorStyle style, SKColor? under)
    {
        // The ordinary pointer is the system's, not ours: whatever the user has
        // set their cursor to is what they expect to see.
        if (style == CursorStyle.Arrow)
        {
            return SystemArrow;
        }

        var dark = under is { } colour && Luminance(colour) > 0.55;
        var cache = dark ? DarkCursors : LightCursors;

        if (!cache.TryGetValue(style, out var cursor))
        {
            cursor = dark
                ? Build(style, SKColors.Black, SKColors.White)
                : Build(style, SKColors.White, SKColors.Black);

            cache[style] = cursor;
        }

        return cursor;
    }

    /// <summary>
    /// The threshold is the same luminance used for the tick on a colour
    /// swatch, so a palette colour and the screen behind it are judged alike.
    /// </summary>
    private static double Luminance(SKColor colour) =>
        ((0.2126 * colour.Red) + (0.7152 * colour.Green) + (0.0722 * colour.Blue)) / 255.0;

    private static Cursor Build(CursorStyle style, SKColor ink, SKColor halo)
    {
        using var path = SKPath.ParseSvgPathData(Shapes[style])
            ?? throw new InvalidOperationException($"Не разобрать контур курсора {style}.");

        path.Transform(SKMatrix.CreateScaleTranslation(
            Scale,
            Scale,
            Centre - (Middle * Scale),
            Centre - (Middle * Scale)));

        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        // Halo first, ink on top: the wide stroke under the thin one is what
        // keeps the shape readable over a busy photograph.
        var smooth = Curved.Contains(style);

        Stroke(canvas, path, halo, 3, smooth: true);
        Stroke(canvas, path, ink, 1, smooth);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        return new Cursor(new Avalonia.Media.Imaging.Bitmap(stream), new PixelPoint(16, 16));
    }

    private static void Stroke(SKCanvas canvas, SKPath path, SKColor colour, float width, bool smooth)
    {
        using var paint = new SKPaint
        {
            Color = colour,
            IsAntialias = smooth,
            IsStroke = true,
            StrokeWidth = width,
            StrokeCap = SKStrokeCap.Butt,
            StrokeJoin = SKStrokeJoin.Round,
        };

        canvas.DrawPath(path, paint);
    }
}
