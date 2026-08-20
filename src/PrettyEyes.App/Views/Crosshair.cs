using Avalonia;
using Avalonia.Input;
using SkiaSharp;

namespace PrettyEyes.App.Views;

/// <summary>
/// Two crosshairs, light and dark, chosen by what is underneath.
///
/// The system cross is a single black shape and disappears against anything
/// dark, which on a screenshot tool means half the screenshots people take.
/// Drawing the cursor ourselves on the canvas would fix the colour and cost a
/// frame of lag on the one thing nobody tolerates lag on, so instead there are
/// two prepared cursors and the caller swaps between them: the swap is a
/// pointer assignment, and the cursor stays drawn by the system.
///
/// Each shape carries a halo of the opposite colour, so the moment of the swap
/// is never a moment of invisibility.
/// </summary>
public static class Crosshair
{
    private const int Size = 32;
    private const int Centre = 16;

    /// <summary>The bare pixel at the centre stays visible: it is the one being aimed at.</summary>
    private const float Gap = 3;

    /// <summary>
    /// How far each arm goes. Short on purpose: a crosshair that spans the whole
    /// cursor box reads as a graphic in its own right, and the job here is to
    /// point at one pixel.
    /// </summary>
    private const float Reach = 8;

    public static Cursor Light { get; } = Build(SKColors.White, SKColors.Black);

    public static Cursor Dark { get; } = Build(SKColors.Black, SKColors.White);

    /// <summary>
    /// Which of the two reads against this colour. The threshold is the same
    /// luminance used for the tick on a colour swatch, so a palette colour and
    /// the screen behind it are judged the same way.
    /// </summary>
    public static Cursor For(SKColor? under) =>
        under is { } colour && Luminance(colour) > 0.55 ? Dark : Light;

    private static double Luminance(SKColor colour) =>
        ((0.2126 * colour.Red) + (0.7152 * colour.Green) + (0.0722 * colour.Blue)) / 255.0;

    private static Cursor Build(SKColor ink, SKColor halo)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        // Halo first, ink on top: the wide dark stroke under a thin light one
        // is what keeps the shape readable over a busy photograph.
        Stroke(canvas, halo, 3);
        Stroke(canvas, ink, 1);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        return new Cursor(new Avalonia.Media.Imaging.Bitmap(stream), new PixelPoint(Centre, Centre));
    }

    private static void Stroke(SKCanvas canvas, SKColor colour, float width)
    {
        using var paint = new SKPaint
        {
            Color = colour,
            IsAntialias = false,
            IsStroke = true,
            StrokeWidth = width,
            StrokeCap = SKStrokeCap.Butt,
        };

        var half = Centre + 0.5f;

        canvas.DrawLine(half - Reach, half, half - Gap, half, paint);
        canvas.DrawLine(half + Gap, half, half + Reach, half, paint);
        canvas.DrawLine(half, half - Reach, half, half - Gap, paint);
        canvas.DrawLine(half, half + Gap, half, half + Reach, paint);
    }
}
