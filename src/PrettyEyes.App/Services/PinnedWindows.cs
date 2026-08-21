using Avalonia;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Pinning;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Tools;
using SkiaSharp;
using CaptureRect = PrettyEyes.Core.Geometry.CaptureRect;

namespace PrettyEyes.App.Services;

/// <summary>
/// The windows half of pinning, over the registry that knows nothing about
/// windows. Opens a pin, keeps the list, and does the things that are done to
/// all of them at once.
/// </summary>
public sealed class PinnedWindows
{
    /// <summary>
    /// How far a pin is nudged when its place is already taken. Enough to see
    /// the edge of the one underneath, small enough that the stack stays where
    /// the area was.
    /// </summary>
    private const int Nudge = 24;

    private readonly PinRegistry _registry = new();

    /// <summary>Whether anything drawn inside a pin would be lost right now.</summary>
    public bool AnyWithAnnotations => _registry.AnyWithAnnotations;

    public int Count => _registry.Count;

    /// <summary>
    /// Pins one area of a document. The pin gets its own image cropped out of
    /// the capture, so it neither holds the whole desktop nor dies with the
    /// overlay.
    /// </summary>
    /// <param name="area">In virtual-desktop pixels, as selected.</param>
    public PinnedWindow Pin(
        Document source,
        CaptureRect area,
        ToolStyles styles,
        ToolVisibility tools,
        bool drawingAllowed,
        Func<SKImage?> glyph)
    {
        var cropped = DocumentRenderer.Crop(source, area);

        // The bounds are the area in the coordinates of the original capture,
        // not zeroes: blur subtracts them when it samples and adds them back
        // when it draws, and with zeroes it would quietly sample the wrong
        // pixels.
        var document = new Document(cropped, area);

        var window = new PinnedWindow { Glyph = glyph };

        window.Gone += (sender, _) =>
        {
            if (sender is IPinned pinned)
            {
                _registry.Remove(pinned);
            }
        };

        _registry.Add(window);
        window.Open(document, Free(area), styles, WithoutText(tools), drawingAllowed);

        return window;
    }

    /// <summary>
    /// The same tools minus the label. Typing needs a caret, a blinking timer
    /// and a mode machine, and a pinned window has none of the three; the
    /// button would be a button that does nothing.
    /// </summary>
    private static ToolVisibility WithoutText(ToolVisibility tools)
    {
        var shown = tools.ToDictionary();
        shown[ToolKind.Text] = false;

        return new ToolVisibility(shown);
    }

    public void HideAll()
    {
        foreach (var window in Windows())
        {
            window.Hide();
        }
    }

    public void ShowAll()
    {
        foreach (var window in Windows())
        {
            window.Show();
        }
    }

    /// <summary>
    /// Closes every pin, asking once if anything drawn would be lost. Once,
    /// not once per window: the answer is the same for all of them, and a
    /// stack of questions is a stack nobody reads.
    /// </summary>
    public void CloseAll()
    {
        if (!AnyWithAnnotations)
        {
            Force();
            return;
        }

        var windows = Windows().ToList();
        var count = windows.Count;

        windows[^1].Question(
            count > 1
                ? $"Закрыть все закреплённые ({count})? Нарисованное в них пропадёт"
                : "Закрыть? Нарисованное здесь пропадёт",
            Force);
    }

    /// <summary>Over a copy: each close takes itself out of the registry.</summary>
    private void Force()
    {
        foreach (var window in Windows())
        {
            window.Close();
        }
    }

    private IEnumerable<PinnedWindow> Windows() => _registry.Pins.OfType<PinnedWindow>();

    /// <summary>
    /// Where the new pin goes. Its own place, unless a pin of exactly that
    /// geometry is already there, in which case it steps aside - and the
    /// question is asked again, or the third pin would land on the second.
    /// </summary>
    private CaptureRect Free(CaptureRect area)
    {
        var place = area;

        while (Taken(place))
        {
            place = new CaptureRect(place.X + Nudge, place.Y + Nudge, place.Width, place.Height);
        }

        return place;
    }

    private bool Taken(CaptureRect place) => Windows().Any(window =>
        window.Position == new PixelPoint(place.X, place.Y)
        && (int)Math.Round(window.Bounds.Width * window.RenderScaling) == place.Width
        && (int)Math.Round(window.Bounds.Height * window.RenderScaling) == place.Height);
}
