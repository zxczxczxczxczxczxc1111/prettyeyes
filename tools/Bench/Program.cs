using System.Diagnostics;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Platform;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Platform.Windows;
using SkiaSharp;

namespace PrettyEyes.Bench;

/// <summary>
/// Numbers for the capture and render paths. Optimisation without a harness in
/// the repository turns into guessing on the next session, and the guesses are
/// always wrong about which part is slow.
///
/// Needs a live desktop: Windows.Graphics.Capture has nothing to capture in CI.
/// </summary>
internal static class Program
{
    private const int Runs = 10;

    /// <summary>The step that wraps all the per-monitor ones.</summary>
    private const string Envelope = "monitor всего";

    private static void Main()
    {
        var monitors = new Win32MonitorEnumerator();
        var layout = monitors.Enumerate();
        var bounds = layout.VirtualBounds;

        Console.WriteLine($"мониторов: {layout.Monitors.Count}, виртуальный стол: {bounds.Width}x{bounds.Height}, "
            + $"кадр {bounds.Width * bounds.Height * 4 / 1024.0 / 1024.0:F1} МБ");

        foreach (var monitor in layout.Monitors)
        {
            Console.WriteLine($"  {monitor.DeviceId} {monitor.Bounds.Width}x{monitor.Bounds.Height} "
                + $"@ {monitor.Bounds.X},{monitor.Bounds.Y}");
        }

        Console.WriteLine();
        Console.WriteLine($"WGC поддерживается: {WgcScreenCapture.IsSupported}");
        Console.WriteLine();

        using var gdi = new DesktopCapture(monitors, [new GdiScreenCapture()]);
        Cold("GDI, первый вызов", () => gdi.CaptureAll().Image.Dispose());
        Measure("GDI CaptureAll", () => gdi.CaptureAll().Image.Dispose());

        using var wgc = new DesktopCapture(monitors, [new WgcScreenCapture()]);
        Cold("WGC, первый вызов (с созданием D3D)", () => wgc.CaptureAll().Image.Dispose());
        Measure("WGC CaptureAll", () => wgc.CaptureAll().Image.Dispose());

        Sample(wgc);
        RenderPath(wgc);
        DrawLoop(wgc);
        Allocations(wgc);
        Breakdown(monitors);
    }

    /// <summary>
    /// Where a capture actually spends its time. The whole-call number says a
    /// capture is slow; only this says which half is worth touching.
    /// </summary>
    private static void Breakdown(IMonitorEnumerator monitors)
    {
        var samples = new Dictionary<string, List<double>>();

        using var capture = new DesktopCapture(monitors, [new WgcScreenCapture(Note)], Note);

        void Note(string name, double ms)
        {
            lock (samples)
            {
                if (!samples.TryGetValue(name, out var list))
                {
                    samples[name] = list = [];
                }

                list.Add(ms);
            }
        }

        // The first call carries device warm-up; it is measured above, not here.
        capture.CaptureAll().Image.Dispose();
        samples.Clear();

        for (var i = 0; i < Runs; i++)
        {
            capture.CaptureAll().Image.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine("из чего состоит снимок WGC (сумма по всем мониторам за один снимок):");

        var total = 0.0;

        foreach (var (name, values) in samples)
        {
            // Per-monitor steps fire once per monitor, so the per-capture cost
            // is the whole run divided by the number of captures.
            var perCapture = values.Sum() / Runs;

            // The envelope step contains the others; adding it to the total
            // would count the same milliseconds twice.
            if (name != Envelope)
            {
                total += perCapture;
            }

            Console.WriteLine($"  {name,-14} {perCapture,6:F1} мс   (вызовов за снимок: {values.Count / (double)Runs:F0})");
        }

        var envelope = samples.TryGetValue(Envelope, out var whole) ? whole.Sum() / Runs : 0;
        var inner = total - samples
            .Where(pair => pair.Key is "surface" or "clear" or "stitch" or "snapshot")
            .Sum(pair => pair.Value.Sum() / Runs);

        Console.WriteLine($"  {"размечено",-14} {total,6:F1} мс");

        // Only meaningful while an envelope step exists, and only when the
        // steps ran one after another: parallel work sums past the wall clock.
        if (envelope > inner)
        {
            Console.WriteLine($"  {"не размечено",-14} {envelope - inner,6:F1} мс");
        }
    }

    /// <summary>
    /// Writes one capture to disk. A capture that is fast and black is not a
    /// capture, and only a picture proves otherwise.
    /// </summary>
    private static void Sample(IScreenCapture capture)
    {
        var shot = capture.CaptureAll();

        using (shot.Image)
        {
            using var data = shot.Image.Encode(SKEncodedImageFormat.Png, 90);
            var path = Path.Combine(Path.GetTempPath(), "prettyeyes-bench-sample.png");
            using var file = File.Create(path);
            data.SaveTo(file);

            Console.WriteLine();
            Console.WriteLine($"снимок сохранён: {path} ({shot.Image.Width}x{shot.Image.Height})");
        }
    }

    /// <summary>
    /// What the copy and save buttons cost, and how the cost grows with blur:
    /// blur is the only annotation whose price is not a rounding error.
    /// </summary>
    private static void RenderPath(IScreenCapture capture)
    {
        var shot = capture.CaptureAll();
        using var document = new Document(shot.Image, shot.Bounds);

        var monitor = shot.Layout.Monitors[0].Bounds;
        document.Selection = new CaptureRect(monitor.X + 100, monitor.Y + 100, 1200, 800);

        Console.WriteLine();

        // The render path is what the copy button uses; a fast render of a
        // black rectangle is not a render.
        using (var rendered = DocumentRenderer.Render(document))
        {
            using var data = rendered.Encode(SKEncodedImageFormat.Png, 90);
            var path = Path.Combine(Path.GetTempPath(), "prettyeyes-bench-render.png");
            using var file = File.Create(path);
            data.SaveTo(file);
            Console.WriteLine($"результат рендера сохранён: {path} ({rendered.Width}x{rendered.Height})");
        }

        Measure("Render 1200x800, без пометок", () => DocumentRenderer.Render(document).Dispose());

        foreach (var count in (int[])[1, 3, 5])
        {
            document.Clear();

            for (var i = 0; i < count; i++)
            {
                document.Add(new BlurAnnotation(
                    new CaptureRect(monitor.X + 150 + (i * 60), monitor.Y + 150, 320, 200)));
            }

            Measure($"Render, областей размытия: {count}", () => DocumentRenderer.Render(document).Dispose());
        }

        document.Clear();

        // What the decoration costs on top of the render, on a full monitor:
        // the aura is a second blur, and it has to stay cheap enough that
        // nobody notices the copy button got slower.
        document.Selection = monitor;

        Measure("Render монитора, без оформления", () =>
            DocumentRenderer.Render(document, ExportStyle.None).Dispose());

        Measure("Render монитора, пресет «карточка»", () =>
            DocumentRenderer.Render(document, ExportStyle.Card).Dispose());

        Measure("Render монитора, пресет «на белом»", () =>
            DocumentRenderer.Render(document, ExportStyle.Sheet).Dispose());

        document.Clear();
    }

    /// <summary>
    /// What a dragged frame costs frame after frame.
    ///
    /// RenderPath above measures one export; this measures the loop: the same
    /// unchanged annotations drawn again and again into an offscreen surface,
    /// which is what the overlay does while the mouse moves. Allocations are
    /// counted with it because the render thread is exactly where a per-frame
    /// allocation turns into a pause somebody sees.
    /// </summary>
    private static void DrawLoop(IScreenCapture capture)
    {
        var shot = capture.CaptureAll();
        using var document = new Document(shot.Image, shot.Bounds);

        var monitor = shot.Layout.Monitors[0].Bounds;

        using var surface = SKSurface.Create(new SKImageInfo(1200, 800))
            ?? throw new InvalidOperationException("Не выделить поверхность 1200x800.");

        // Annotations live in virtual-desktop coordinates, and the left monitor
        // starts at a negative origin. Without this the whole squiggle lands
        // outside the surface, Skia clips all of it, and the measurement is of
        // an empty canvas. DocumentRenderer.Crop does the same translation for
        // the same reason.
        surface.Canvas.Translate(-monitor.X - 100, -monitor.Y - 100);

        Console.WriteLine();

        foreach (var (count, highlighter) in ((int Count, bool Highlighter)[])
            [(1, false), (5, false), (20, false), (5, true)])
        {
            document.Clear();

            for (var i = 0; i < count; i++)
            {
                document.Add(new StrokeAnnotation(
                    Squiggle(monitor.X + 150 + (i * 7), monitor.Y + 150, 300),
                    0xFFE5484D,
                    strokeWidth: highlighter ? 12 : 3,
                    highlighter));
            }

            var annotations = document.SnapshotAnnotations();

            void Frame()
            {
                foreach (var annotation in annotations)
                {
                    annotation.Draw(surface.Canvas, document.Source, document.SourceBounds, document.BlurCache);
                }
            }

            // Warm first: the first draw of anything pays for whatever Skia
            // sets up lazily, and that is not what a frame in the middle of a
            // drag looks like.
            Frame();

            var before = GC.GetTotalAllocatedBytes(precise: true);
            Measure($"цикл, росчерков {count}{(highlighter ? ", маркер" : "")}", Frame);
            var after = GC.GetTotalAllocatedBytes(precise: true);

            Console.WriteLine($"{"",-38} аллокаций за {Runs} кадров {(after - before) / 1024.0:F1} КБ");
        }
    }

    /// <summary>
    /// A hand-drawn line of the shape people actually draw: three hundred
    /// points with a wobble, which is what a second of dragging reports.
    /// </summary>
    private static List<(int X, int Y)> Squiggle(int x, int y, int points)
    {
        var line = new List<(int X, int Y)>(points);

        for (var i = 0; i < points; i++)
        {
            line.Add((x + i, y + (int)(Math.Sin(i / 9.0) * 40)));
        }

        return line;
    }

    /// <summary>
    /// Managed bytes per capture. Anything above a megabyte here means a large
    /// array on the heap that the collector has to deal with on every hotkey.
    /// </summary>
    private static void Allocations(IScreenCapture capture)
    {
        var before = GC.GetTotalAllocatedBytes(precise: true);
        capture.CaptureAll().Image.Dispose();
        var after = GC.GetTotalAllocatedBytes(precise: true);

        Console.WriteLine();
        Console.WriteLine($"аллокаций на снимок: {(after - before) / 1024.0 / 1024.0:F1} МБ");
    }

    private static void Cold(string name, Action body)
    {
        var watch = Stopwatch.StartNew();
        body();
        watch.Stop();
        Console.WriteLine($"{name,-38} {watch.Elapsed.TotalMilliseconds,6:F1} мс");
    }

    private static void Measure(string name, Action body)
    {
        var times = new List<double>(Runs);

        for (var i = 0; i < Runs; i++)
        {
            var watch = Stopwatch.StartNew();
            body();
            watch.Stop();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();

        Console.WriteLine($"{name,-38} мин {times[0],6:F1}   медиана {times[Runs / 2],6:F1}   макс {times[^1],6:F1} мс");
    }
}
