using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using SkiaSharp;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Turns a document into a flat image. The single path used by both
/// "copy to clipboard" and "save to file", so effects are always baked in.
/// </summary>
public static class DocumentRenderer
{
    /// <summary>
    /// Three shadows, not one: contact, middle and far.
    ///
    /// One blur can be soft or it can be tight, and depth needs both at once -
    /// a dark line where the card meets the surface, a body under it, and a
    /// wide haze that says how high above the surface it is. Every number is a
    /// share of the padding, and the far layer is kept inside it: a shadow that
    /// reaches past the canvas is cut off by a straight edge and reads as a
    /// dark border around the picture.
    /// </summary>
    private static readonly (float Offset, float Sigma, byte Alpha)[] Shadows =
    [
        (0.04f, 0.08f, 90),
        (0.14f, 0.22f, 70),
        (0.30f, 0.35f, 60),
    ];

    /// <summary>Short side of the copy the aura is built from.</summary>
    private const int AuraThumbnail = 64;

    /// <summary>Blur radius on that copy, not on the screenshot.</summary>
    private const float AuraSigma = 12f;

    /// <summary>How far past the canvas the haze is stretched.</summary>
    private const float AuraSpread = 1.6f;

    /// <summary>Below this the screenshot counts as dark and the haze is lifted.</summary>
    private const double AuraThreshold = 0.5;

    private const byte AuraLift = 90;

    private const byte AuraShade = 60;

    /// <summary>Strongest the light at the top of the card is, out of 255.</summary>
    private const byte SheenAlpha = 12;

    /// <summary>The bright pixel around the card.</summary>
    private const byte RimAlpha = 28;

    /// <summary>Side of the noise tile the grain is repeated from.</summary>
    private const int GrainTile = 128;

    /// <summary>Strongest a single speck is allowed to be, out of 255.</summary>
    private const byte GrainAlpha = 14;

    public static SKImage Render(Document document) => Render(document, ExportStyle.None);

    public static SKImage Render(Document document, ExportStyle style)
    {
        var frame = document.SourceBounds;

        var selection = document.Selection.IsEmpty
            ? frame
            : document.Selection.Intersect(frame);

        if (selection.IsEmpty)
        {
            throw new InvalidOperationException("Selection lies entirely outside the captured frame.");
        }

        var shot = Flatten(document, frame, selection);
        var fitted = style.FitTo(selection.Width, selection.Height);

        // Without decoration the caller owns the screenshot as it is.
        if (!fitted.Enabled)
        {
            return shot;
        }

        // With decoration it is an intermediate, and it dies here.
        using (shot)
        {
            return Decorate(shot, fitted);
        }
    }

    /// <summary>The screenshot itself: the captured pixels plus what was drawn on them.</summary>
    private static SKImage Flatten(Document document, CaptureRect frame, CaptureRect selection)
    {
        var info = new SKImageInfo(selection.Width, selection.Height);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {selection.Width}x{selection.Height} surface.");

        var canvas = surface.Canvas;

        // Work in virtual-desktop coordinates, then crop by translating.
        canvas.Translate(-selection.X, -selection.Y);
        canvas.DrawImage(document.Source, frame.X, frame.Y);

        foreach (var annotation in document.SnapshotAnnotations())
        {
            annotation.Draw(canvas, document.Source, frame);
        }

        return surface.Snapshot();
    }

    /// <summary>Padding, backdrop, rounded corners and a shadow, in that order.</summary>
    private static SKImage Decorate(SKImage shot, ExportStyle style)
    {
        var width = shot.Width + (style.Padding * 2);
        var height = shot.Height + (style.Padding * 2);

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {width}x{height} surface.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, style, shot, width, height);

        var destination = SKRect.Create(style.Padding, style.Padding, shot.Width, shot.Height);
        using var rounded = new SKRoundRect(destination, style.CornerRadius);

        if (style.Shadow)
        {
            DrawShadow(canvas, rounded, style.Padding);
        }

        // The shadow goes down first and the screenshot lands on top of it,
        // opaque. Drawing the screenshot through the shadow filter instead -
        // which is what CreateDropShadow does - would put a second and a third
        // copy of it on the canvas: edges thicken and anything half transparent
        // that was drawn on the screenshot turns solid.
        canvas.Save();

        if (style.CornerRadius > 0)
        {
            canvas.ClipRoundRect(rounded, antialias: true);
        }

        canvas.DrawImage(shot, destination, new SKSamplingOptions(SKFilterMode.Linear));

        if (style.Sheen)
        {
            DrawSheen(canvas, rounded, destination);
        }

        canvas.Restore();

        return surface.Snapshot();
    }

    private static void DrawBackground(SKCanvas canvas, ExportStyle style, SKImage shot, int width, int height)
    {
        DrawBackdrop(canvas, style, shot, width, height);

        if (style.GrainAllowed)
        {
            DrawGrain(canvas, width, height);
        }
    }

    private static void DrawBackdrop(SKCanvas canvas, ExportStyle style, SKImage shot, int width, int height)
    {
        var area = SKRect.Create(0, 0, width, height);

        switch (style.Background)
        {
            case ExportBackground.Aura:
                DrawAura(canvas, shot, width, height);
                return;

            case ExportBackground.Transparent:
                return;

            case ExportBackground.White:
                canvas.Clear(SKColors.White);
                return;

            case ExportBackground.Gradient:
                // Two greys from the same family as the interface: a backdrop
                // that says "this is a screenshot" without competing with it.
                using (var shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(width, height),
                    [new SKColor(0x1C, 0x1C, 0x20), new SKColor(0x0A, 0x0A, 0x0B)],
                    SKShaderTileMode.Clamp))
                {
                    using var paint = new SKPaint { Shader = shader };
                    canvas.DrawRect(area, paint);
                }

                return;

            default:
                canvas.Clear(SKColors.Black);
                return;
        }
    }

    /// <summary>
    /// Light from above, and an edge to catch it.
    ///
    /// A flat rectangle on a backdrop is a rectangle; the same rectangle a
    /// shade lighter at the top and outlined by one bright pixel is an object
    /// lying on something. Both parts are deliberately at the edge of visible:
    /// the moment either is noticeable on its own, the screenshot has been
    /// tampered with rather than presented.
    /// </summary>
    private static void DrawSheen(SKCanvas canvas, SKRoundRect shape, SKRect card)
    {
        using (var shader = SKShader.CreateLinearGradient(
            new SKPoint(card.Left, card.Top),
            new SKPoint(card.Left, card.Top + (card.Height / 2f)),
            [new SKColor(255, 255, 255, SheenAlpha), new SKColor(255, 255, 255, 0)],
            SKShaderTileMode.Clamp))
        {
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(card, paint);
        }

        using var rim = new SKPaint
        {
            Color = new SKColor(255, 255, 255, RimAlpha),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true,
        };

        // Inset by half a pixel: the clip cuts the outer half of a stroke laid
        // exactly on the edge, and a rim half as bright as asked for is a rim
        // nobody can tune.
        using var inner = new SKRoundRect(shape);
        inner.Deflate(0.5f, 0.5f);

        canvas.DrawRoundRect(inner, rim);
    }

    private static void DrawShadow(SKCanvas canvas, SKRoundRect shape, int padding)
    {
        var filters = new SKImageFilter[Shadows.Length];

        try
        {
            for (var i = 0; i < Shadows.Length; i++)
            {
                var (offset, sigma, alpha) = Shadows[i];

                filters[i] = SKImageFilter.CreateDropShadowOnly(
                    0,
                    padding * offset,
                    padding * sigma,
                    padding * sigma,
                    new SKColor(0, 0, 0, alpha));
            }

            using var merged = SKImageFilter.CreateMerge(filters);
            using var paint = new SKPaint { ImageFilter = merged, IsAntialias = true };

            // The silhouette, not the screenshot: blurring a rounded rectangle
            // three times costs nothing next to blurring three and a half
            // million pixels three times.
            canvas.DrawRoundRect(shape, paint);
        }
        finally
        {
            // Native objects, and there are three of them on every export.
            foreach (var filter in filters)
            {
                filter?.Dispose();
            }
        }
    }

    /// <summary>
    /// Film grain over the backdrop.
    ///
    /// Two jobs. It breaks up the banding a big soft gradient always has on an
    /// eight bit screen, and it makes the backdrop read as a material instead
    /// of a fill. Never over the screenshot itself: the screenshot is evidence,
    /// and evidence does not get texture applied to it.
    /// </summary>
    private static void DrawGrain(SKCanvas canvas, int width, int height)
    {
        using var shader = Grain.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        using var paint = new SKPaint { Shader = shader };

        canvas.DrawRect(SKRect.Create(0, 0, width, height), paint);
    }

    /// <summary>
    /// One tile, built once, the same on every machine and every run.
    ///
    /// Deliberately not Random and not Skia's Perlin noise: the first makes two
    /// exports of one screenshot differ, and the second gives no promise that
    /// its seed means the same thing in the next version of Skia.
    /// </summary>
    private static readonly SKImage Grain = BuildGrain();

    private static SKImage BuildGrain()
    {
        var info = new SKImageInfo(GrainTile, GrainTile, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);

        for (var y = 0; y < GrainTile; y++)
        {
            for (var x = 0; x < GrainTile; x++)
            {
                // A hash of the coordinates, not a sequence: the tile is then
                // the same however it is walked.
                var hash = (uint)((x * 73856093) ^ (y * 19349663));
                hash ^= hash >> 13;
                hash *= 2654435761;
                hash ^= hash >> 16;

                // Half the specks lighten, half darken, so the backdrop keeps
                // the lightness it was given.
                var light = (hash & 1) == 0;
                var alpha = (byte)(hash % (GrainAlpha + 1));
                var tone = light ? (byte)255 : (byte)0;

                bitmap.SetPixel(x, y, new SKColor(tone, tone, tone, alpha));
            }
        }

        bitmap.SetImmutable();

        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>
    /// The screenshot's own blurred copy, blown up past the edges.
    ///
    /// A gradient is a decision somebody made once for every screenshot there
    /// will ever be; this one is made by the picture itself, so the colours
    /// behind it are always its own.
    /// </summary>
    private static void DrawAura(SKCanvas canvas, SKImage shot, int width, int height)
    {
        using var thumbnail = Thumbnail(shot);
        using var haze = Blur(thumbnail);

        var (average, lightness) = Measure(thumbnail);

        // The flat average goes down first. A blur of sixty-four pixels with
        // this much sigma bleeds alpha out of its own edges, and without an
        // opaque base underneath, a backdrop nobody asked to be transparent
        // comes out see-through at the corners. Measured, not feared.
        using (var base_ = new SKPaint { Color = average })
        {
            canvas.DrawRect(SKRect.Create(0, 0, width, height), base_);
        }

        // Bigger than the canvas so the blurred edges stay off screen: a blur
        // clamped at the border leaves a visible seam along it.
        var spread = SKRect.Create(
            (width - (width * AuraSpread)) / 2f,
            (height - (height * AuraSpread)) / 2f,
            width * AuraSpread,
            height * AuraSpread);

        // Linear with mipmaps, because this is a 64 pixel image stretched
        // across a whole canvas: the default in SkiaSharp 3 is nearest
        // neighbour, and that turns the haze into forty pixel squares.
        canvas.DrawImage(haze, spread, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        // Away from the screenshot's own lightness, never towards it. A dark
        // shot on a dark haze is the illness this whole thing is curing, and a
        // white page on a white haze is the same illness from the other side.
        var veil = lightness < AuraThreshold
            ? new SKColor(255, 255, 255, AuraLift)
            : new SKColor(0, 0, 0, AuraShade);

        using var paint = new SKPaint { Color = veil };
        canvas.DrawRect(SKRect.Create(0, 0, width, height), paint);
    }

    /// <summary>
    /// A copy small enough that blurring it is free. Only ever smaller: a forty
    /// pixel selection blown up to sixty-four before being blurred would be
    /// paying for detail that is not there.
    /// </summary>
    private static SKImage Thumbnail(SKImage shot)
    {
        var shorter = Math.Min(shot.Width, shot.Height);
        var scale = Math.Min(1f, (float)AuraThumbnail / shorter);

        var width = Math.Max(1, (int)(shot.Width * scale));
        var height = Math.Max(1, (int)(shot.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height))
            ?? throw new InvalidOperationException($"Could not allocate a {width}x{height} surface.");

        surface.Canvas.DrawImage(
            shot,
            SKRect.Create(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        return surface.Snapshot();
    }

    private static SKImage Blur(SKImage thumbnail)
    {
        using var surface = SKSurface.Create(new SKImageInfo(thumbnail.Width, thumbnail.Height))
            ?? throw new InvalidOperationException("Could not allocate a surface for the aura.");

        using var filter = SKImageFilter.CreateBlur(AuraSigma, AuraSigma, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { ImageFilter = filter };

        surface.Canvas.DrawImage(thumbnail, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);

        return surface.Snapshot();
    }

    /// <summary>
    /// The screenshot's average colour and how light it is, both from the
    /// thumbnail that already exists. Reading the full picture would be three
    /// and a half million pixels for two numbers a sixty-four pixel copy
    /// answers just as well.
    /// </summary>
    private static (SKColor Average, double Lightness) Measure(SKImage thumbnail)
    {
        using var pixels = thumbnail.PeekPixels();

        if (pixels is null)
        {
            return (SKColors.Black, 0);
        }

        double red = 0, green = 0, blue = 0;
        var counted = 0;

        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var colour = pixels.GetPixelColor(x, y);

                red += colour.Red;
                green += colour.Green;
                blue += colour.Blue;
                counted++;
            }
        }

        if (counted == 0)
        {
            return (SKColors.Black, 0);
        }

        red /= counted;
        green /= counted;
        blue /= counted;

        // Rec. 601, the same weights the crosshair and the highlighter use.
        var lightness = ((red * 0.299) + (green * 0.587) + (blue * 0.114)) / 255.0;

        return (new SKColor((byte)red, (byte)green, (byte)blue), lightness);
    }
}
