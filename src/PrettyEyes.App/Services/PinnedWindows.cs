using Avalonia;
using PrettyEyes.App.Views;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Pinning;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Stats;
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

    /// <summary>
    /// Late-bound the same way the folder sink is: the settings record is
    /// replaced whole on every change, so a captured copy goes stale on the
    /// first edit.
    /// </summary>
    private AppServices? _services;

    public void Use(AppServices services) => _services = services;

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
        Func<SKImage?> glyph)
    {
        var settings = _services?.Settings ?? AppSettings.Default;

        var cropped = DocumentRenderer.Crop(source, area);

        // The bounds are the area in the coordinates of the original capture,
        // not zeroes: blur subtracts them when it samples and adds them back
        // when it draws, and with zeroes it would quietly sample the wrong
        // pixels.
        var document = new Document(cropped, area);

        _services?.Shots.Record(ShotTarget.Pin);

        var window = new PinnedWindow { Glyph = glyph };

        window.Gone += (sender, _) =>
        {
            if (sender is IPinned pinned)
            {
                _registry.Remove(pinned);
            }
        };

        window.CopyRequested += async (sender, _) => await SendAsync(sender, _services?.Clipboard);
        window.SaveRequested += async (sender, _) => await SendAsync(sender, _services?.File);

        _registry.Add(window);

        window.Open(
            document,
            Free(area),
            styles,
            WithoutText(new ToolVisibility(settings.Tools)),
            settings.DrawOnPinned,
            settings.PinOpacity);

        // After Open: there is no window handle to speak of before it is shown.
        window.HideFromCapture(settings.HidePinnedOnCapture);

        return window;
    }

    /// <summary>
    /// The frame out of a pin, into the clipboard or into a file.
    ///
    /// Quick save is deliberately not triggered: a pin lives for hours, and a
    /// folder filling up with copies of the same window is not what that switch
    /// promised.
    /// </summary>
    private async Task SendAsync(object? sender, IImageSink? sink)
    {
        if (sender is not PinnedWindow window || sink is null)
        {
            return;
        }

        try
        {
            using var image = window.Snapshot(_services?.Settings.Export ?? ExportStyle.None);

            if (image is null)
            {
                return;
            }

            await sink.SendAsync(image, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // An async handler is the one place an exception has nowhere to go.
            Log.Default.Error("не удалось отдать закреплённый кадр", ex);
        }
    }

    /// <summary>
    /// Puts back any pin whose centre ended up outside every monitor.
    ///
    /// It is moved rather than closed: what it shows exists nowhere else, and a
    /// window that cannot be reached cannot be saved either.
    /// </summary>
    public void Rehome(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return;
        }

        foreach (var window in Windows())
        {
            var width = (int)Math.Round(window.Bounds.Width * window.RenderScaling);
            var height = (int)Math.Round(window.Bounds.Height * window.RenderScaling);
            var centreX = window.Position.X + (width / 2);
            var centreY = window.Position.Y + (height / 2);

            if (monitors.Any(monitor => monitor.Bounds.Contains(centreX, centreY)))
            {
                continue;
            }

            var home = monitors
                .OrderBy(monitor => Distance(monitor.Bounds, centreX, centreY))
                .First()
                .Bounds;

            window.Position = new PixelPoint(
                Math.Clamp(window.Position.X, home.X, Math.Max(home.X, home.X + home.Width - width)),
                Math.Clamp(window.Position.Y, home.Y, Math.Max(home.Y, home.Y + home.Height - height)));
        }
    }

    private static long Distance(CaptureRect monitor, int x, int y)
    {
        long dx = Math.Max(monitor.X - x, Math.Max(0, x - (monitor.X + monitor.Width)));
        long dy = Math.Max(monitor.Y - y, Math.Max(0, y - (monitor.Y + monitor.Height)));

        return (dx * dx) + (dy * dy);
    }

    /// <summary>Whether every pin is currently kept out of screen captures.</summary>
    public void HideFromCapture(bool hidden)
    {
        foreach (var window in Windows())
        {
            window.HideFromCapture(hidden);
        }
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

            // Showing a window that is already shown is how it is asked to come
            // back from the tray, and a pin was once found sitting behind
            // everything with its topmost flag gone. Saying it again here costs
            // one system call and closes the whole class of that.
            window.KeepOnTop();
        }
    }

    /// <summary>
    /// Closes every pin, asking once if anything drawn would be lost. Once,
    /// not once per window: the answer is the same for all of them, and a
    /// stack of questions is a stack nobody reads.
    /// </summary>
    public void CloseAll()
    {
        var count = _registry.Count;

        var asked = AskFirst(
            count > 1
                ? $"Закрыть все закреплённые ({count})? Нарисованное в них пропадёт"
                : "Закрыть? Нарисованное здесь пропадёт",
            Force);

        if (!asked)
        {
            Force();
        }
    }

    /// <summary>
    /// Asks the question in the topmost pin when there is anything to lose, and
    /// says whether it did. False means nobody was asked and the caller may go
    /// ahead: no drawings, nothing to warn about.
    ///
    /// The question lives in a pin rather than in a window of its own: it is
    /// about those windows, and the app has no other place to put a dialog.
    /// </summary>
    public bool AskFirst(string question, Action yes)
    {
        if (!AnyWithAnnotations)
        {
            return false;
        }

        Windows().Last().Question(question, yes);

        return true;
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
