using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.Core.Annotations;

/// <summary>
/// A line drawn by hand, as a pencil or as a highlighter.
///
/// One annotation for both because they differ in paint and nothing else: the
/// pencil puts opaque ink on top, the highlighter multiplies a wide translucent
/// band into what is underneath, which is what makes text stay readable through
/// it. Two classes would mean the same path smoothing twice.
/// </summary>
public sealed class StrokeAnnotation : IAnnotation
{
    /// <summary>How much wider a highlighter is than a pencil of the same size.</summary>
    public const float HighlighterWidth = 4f;

    /// <summary>
    /// Translucent enough to read through, opaque enough to be a mark. Above
    /// roughly a third the text underneath starts to go.
    /// </summary>
    private const byte PaperAlpha = 90;

    /// <summary>
    /// On a dark screenshot the band lights up instead of soaking in, and it
    /// needs more of the colour to read as a mark at all.
    /// </summary>
    private const byte GlowAlpha = 110;

    /// <summary>Above this the background counts as paper rather than screen.</summary>
    private const double PaperLuminance = 0.5;

    private readonly int[] _x;
    private readonly int[] _y;
    private readonly uint _color;
    private readonly float _strokeWidth;
    private readonly bool _highlighter;

    public StrokeAnnotation(
        IReadOnlyList<(int X, int Y)> points, uint color, float strokeWidth, bool highlighter)
    {
        // Copied on the way in: the tool keeps drawing into its own list while
        // this one is already on the canvas.
        _x = new int[points.Count];
        _y = new int[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            _x[i] = points[i].X;
            _y[i] = points[i].Y;
        }

        _color = color;
        _strokeWidth = highlighter ? strokeWidth * HighlighterWidth : strokeWidth;
        _highlighter = highlighter;

        var pad = (int)Math.Ceiling(_strokeWidth / 2f) + 1;
        var left = _x.Min();
        var top = _y.Min();

        Bounds = new CaptureRect(
            left - pad,
            top - pad,
            _x.Max() - left + (pad * 2),
            _y.Max() - top + (pad * 2));
    }

    public CaptureRect Bounds { get; }

    public void Draw(SKCanvas canvas, SKImage source, CaptureRect sourceOrigin, BlurCache cache)
    {
        using var path = BuildPath();

        // A highlighter is a translucent ink, so what it does depends on what
        // is under it. On paper multiplying is exactly right: black text stays
        // black. On a dark interface multiplying is invisible - measured, not
        // guessed - so there the band lights the area up instead.
        var paper = _highlighter && OverPaper(source, sourceOrigin);

        using var paint = new SKPaint
        {
            Color = _highlighter
                ? new SKColor(_color).WithAlpha(paper ? PaperAlpha : GlowAlpha)
                : new SKColor(_color),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            BlendMode = _highlighter
                ? paper ? SKBlendMode.Multiply : SKBlendMode.Screen
                : SKBlendMode.SrcOver,
            IsAntialias = true,
        };

        // One stroke for the whole path on purpose. Drawn segment by segment,
        // a translucent line darkens at every joint and the hand-drawn look
        // turns into a string of beads.
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Whether the stroke lies on something light. A couple of dozen samples
    /// along the line, which is enough to tell a page from an editor and cheap
    /// enough to redo on every pointer move.
    /// </summary>
    private bool OverPaper(SKImage source, CaptureRect sourceOrigin)
    {
        using var pixels = source.PeekPixels();

        if (pixels is null)
        {
            return false;
        }

        const int Samples = 24;

        var step = Math.Max(1, _x.Length / Samples);
        var total = 0.0;
        var counted = 0;

        for (var i = 0; i < _x.Length; i += step)
        {
            var px = _x[i] - sourceOrigin.X;
            var py = _y[i] - sourceOrigin.Y;

            if (px < 0 || py < 0 || px >= pixels.Width || py >= pixels.Height)
            {
                continue;
            }

            var colour = pixels.GetPixelColor(px, py);

            // Rec. 601, the same weights the crosshair picks its ink with.
            total += ((colour.Red * 0.299) + (colour.Green * 0.587) + (colour.Blue * 0.114)) / 255.0;
            counted++;
        }

        return counted > 0 && total / counted > PaperLuminance;
    }

    /// <summary>
    /// Points to a curve. Each segment runs to the midpoint of the next one
    /// with the point itself as the control, which is the cheapest smoothing
    /// there is and turns the polyline the mouse reports into something that
    /// looks drawn rather than plotted.
    /// </summary>
    private SKPath BuildPath()
    {
        var path = new SKPath();

        if (_x.Length == 1)
        {
            // A tap still leaves a dot: a zero-length path strokes nothing.
            path.MoveTo(_x[0], _y[0]);
            path.LineTo(_x[0] + 0.01f, _y[0]);

            return path;
        }

        path.MoveTo(_x[0], _y[0]);

        for (var i = 1; i < _x.Length - 1; i++)
        {
            var midX = (_x[i] + _x[i + 1]) / 2f;
            var midY = (_y[i] + _y[i + 1]) / 2f;

            path.QuadTo(_x[i], _y[i], midX, midY);
        }

        path.LineTo(_x[^1], _y[^1]);

        return path;
    }
}
