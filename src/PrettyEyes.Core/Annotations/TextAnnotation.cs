using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Text;
using PrettyEyes.Core.Tools;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// Words put on the screenshot.
///
/// Immutable like every other annotation, and its lines are laid out once in
/// the constructor: the overlay asks for Bounds on every frame while the label
/// is dragged, and re-measuring text sixty times a second to answer the same
/// question is a waste with a visible cost.
/// </summary>
public sealed class TextAnnotation : IMovable
{
    /// <summary>How much one turn of the wheel is worth, in points.</summary>
    public const int SizeStep = 2;

    public TextAnnotation(string text, int x, int y, int? maxWidth, ToolStyle style)
    {
        Text = text;
        MaxWidth = maxWidth;
        Style = style;

        using var font = TextLayout.FontFor(style);

        Segments = TextLayout.Segments(text, font, maxWidth);
        Bounds = TextLayout.Measure(Segments, font, style.TextPadding) with { X = x, Y = y };
    }

    public string Text { get; }

    /// <summary>
    /// The drawn lines and where each starts in the text. Laid out once here so
    /// that the caret, the mouse and the glyphs all agree on the same numbers.
    /// </summary>
    public IReadOnlyList<TextSegment> Segments { get; }

    /// <summary>
    /// The width the text wraps at, or null when the label grows with whatever
    /// is typed. Set by dragging a box instead of clicking a point.
    /// </summary>
    public int? MaxWidth { get; }

    public ToolStyle Style { get; }

    public CaptureRect Bounds { get; }

    public IMovable MovedBy(int dx, int dy) =>
        new TextAnnotation(Text, Bounds.X + dx, Bounds.Y + dy, MaxWidth, Style);

    public IMovable? ResizedBy(int steps)
    {
        var size = Math.Clamp(
            Style.FontSize + (steps * SizeStep),
            ToolStyle.MinFontSize,
            ToolStyle.MaxFontSize);

        if (size == Style.FontSize)
        {
            return null;
        }

        var resized = new TextAnnotation(Text, Bounds.X, Bounds.Y, MaxWidth, Style with { FontSize = size });

        // Around the middle, same as the glyph tool: type that grows from its
        // top-left corner walks away from the thing it was pointing at.
        return resized.MovedBy(
            (Bounds.Width - resized.Bounds.Width) / 2,
            (Bounds.Height - resized.Bounds.Height) / 2);
    }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin) => Draw(canvas, underlay: null);

    /// <summary>
    /// The same drawing with something painted between the plate and the
    /// glyphs. That gap is where a selection highlight belongs: over it and the
    /// highlight hides the very text being selected, under the plate and it is
    /// not there at all.
    /// </summary>
    public void Draw(SKCanvas canvas, Action<SKCanvas, SKFont>? underlay)
    {
        if (Bounds.IsEmpty)
        {
            // Nothing to put a highlight behind either: an empty label is only
            // ever a caret, and whoever is drawing that owns it.
            return;
        }

        using var font = TextLayout.FontFor(Style);
        using var fill = new SKPaint { Color = new SKColor(Style.Color), IsAntialias = true };

        var contrast = Contrast(Style.Color);

        if (Style.TextBackdrop == TextBackdrop.Plate)
        {
            using var plate = new SKPaint { Color = contrast.WithAlpha(200), IsAntialias = true };

            canvas.DrawRoundRect(
                SKRect.Create(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height),
                Style.TextPadding,
                Style.TextPadding,
                plate);
        }

        underlay?.Invoke(canvas, font);

        using var outline = new SKPaint
        {
            Color = contrast,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,

            // Scaled with the type: a fixed hairline disappears at 200 points
            // and swallows the glyph at 8.
            StrokeWidth = Math.Max(1f, Style.FontSize / 8f),
            StrokeJoin = SKStrokeJoin.Round,
        };

        var left = Bounds.X + Style.TextPadding;

        // Ascent is negative, so subtracting it walks down from the top of the
        // box to the first baseline.
        var baseline = Bounds.Y + Style.TextPadding - font.Metrics.Ascent;

        foreach (var segment in Segments)
        {
            var line = segment.Text;

            if (Style.TextBackdrop == TextBackdrop.Outline)
            {
                canvas.DrawText(line, left, baseline, font, outline);
            }

            canvas.DrawText(line, left, baseline, font, fill);
            baseline += font.Spacing;
        }
    }

    /// <summary>
    /// Black behind light text, white behind dark text. Crude on purpose: the
    /// point is that the label is readable, not that it is pretty.
    /// </summary>
    private static SKColor Contrast(uint color)
    {
        var rgb = new SKColor(color);
        var luminance = ((0.299 * rgb.Red) + (0.587 * rgb.Green) + (0.114 * rgb.Blue)) / 255d;

        return luminance > 0.5 ? SKColors.Black : SKColors.White;
    }
}
