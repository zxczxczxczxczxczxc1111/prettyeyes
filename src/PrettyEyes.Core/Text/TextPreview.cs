using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Tools;
using SkiaSharp;

namespace PrettyEyes.Core.Text;

/// <summary>
/// A label while it is still being typed: the label itself, the selection
/// behind its glyphs and the caret on top.
///
/// An IAnnotation because the overlay already has a channel for "the shape the
/// gesture would produce" and draws it above the document without storing it.
/// Nothing here ever reaches the screenshot.
/// </summary>
public sealed class TextPreview : IAnnotation
{
    /// <summary>Two physical pixels, so it survives on a high-DPI screen.</summary>
    private const int CaretWidth = 2;

    private readonly TextAnnotation _label;
    private readonly TextEditor _editor;
    private readonly bool _caretOn;

    public TextPreview(TextAnnotation label, TextEditor editor, bool caretOn)
    {
        _label = label;
        _editor = editor;
        _caretOn = caretOn;

        using var font = TextLayout.FontFor(label.Style);

        // An empty label has no box of its own, and the caret has to live
        // somewhere: without this the very first keystroke has nowhere to go.
        Bounds = label.Bounds.IsEmpty
            ? new CaptureRect(
                label.Bounds.X,
                label.Bounds.Y,
                (label.Style.TextPadding * 2) + CaretWidth,
                (label.Style.TextPadding * 2) + (int)Math.Ceiling(font.Spacing))
            : label.Bounds;
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin)
    {
        _label.Draw(canvas, DrawSelection);

        if (!_caretOn)
        {
            return;
        }

        using var font = TextLayout.FontFor(_label.Style);
        using var paint = new SKPaint { Color = CaretColour, IsAntialias = false };

        var caret = TextLayout.CaretAt(_label.Segments, _editor.Caret, font, _label.Style.TextPadding);

        canvas.DrawRect(
            SKRect.Create(
                _label.Bounds.X + caret.X,
                _label.Bounds.Y + caret.Y,
                CaretWidth,
                caret.Height),
            paint);
    }

    /// <summary>
    /// The same colour the glyphs are drawn in: the caret is the text, one
    /// character early.
    /// </summary>
    private SKColor CaretColour => new(_label.Style.Color);

    private void DrawSelection(SKCanvas canvas, SKFont font)
    {
        if (!_editor.HasSelection)
        {
            return;
        }

        var start = _editor.SelectionStart;
        var end = start + _editor.SelectionLength;
        var padding = _label.Style.TextPadding;

        using var paint = new SKPaint { Color = CaretColour.WithAlpha(90), IsAntialias = false };

        for (var line = 0; line < _label.Segments.Count; line++)
        {
            var segment = _label.Segments[line];
            var from = Math.Max(start, segment.Start);
            var to = Math.Min(end, segment.End);

            if (to < from)
            {
                continue;
            }

            // A line whose break is inside the selection is highlighted to its
            // end plus a stub, otherwise a selected newline looks like nothing.
            var trailing = end > segment.End && line + 1 < _label.Segments.Count ? padding : 0;
            var left = Width(font, segment, from - segment.Start);
            var right = Width(font, segment, to - segment.Start) + trailing;

            if (right <= left)
            {
                continue;
            }

            canvas.DrawRect(
                SKRect.Create(
                    _label.Bounds.X + padding + left,
                    _label.Bounds.Y + padding + (line * font.Spacing),
                    right - left,
                    font.Spacing),
                paint);
        }
    }

    private static float Width(SKFont font, TextSegment segment, int offset) =>
        offset <= 0 ? 0f : font.MeasureText(segment.Text.AsSpan(0, Math.Min(offset, segment.Text.Length)));
}
