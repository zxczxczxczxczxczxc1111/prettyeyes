using System.Text;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Reads the rectangles of the windows on screen, front to back.
///
/// Taken in the same breath as the screenshot and never again: the moment the
/// overlay is up, every point on every monitor belongs to us, and asking
/// Windows what is under the pointer would answer with the overlay.
/// </summary>
public static class Win32WindowShapes
{
    /// <summary>
    /// Long enough for the class names that matter here; a name that does not
    /// fit is truncated rather than missed, and nothing below matches on a
    /// prefix.
    /// </summary>
    private const int ClassNameLength = 64;

    /// <summary>
    /// The desktop itself. It covers every monitor, so without taking it out a
    /// double click on bare desktop would select the whole virtual desktop
    /// rather than the monitor under the pointer.
    ///
    /// Two names because the shell moved the wallpaper into a WorkerW sibling
    /// when Active Desktop went away, and which one is in front depends on
    /// whether a slideshow is running.
    /// </summary>
    private static readonly string[] Desktop = ["Progman", "WorkerW"];

    /// <summary>
    /// Every window a person could point at, frontmost first, clipped to the
    /// desktop the screenshot covers.
    /// </summary>
    public static WindowShapes Snapshot(CaptureRect desktop)
    {
        var ours = (uint)Environment.ProcessId;
        var shapes = new List<CaptureRect>();
        var name = new StringBuilder(ClassNameLength);

        // Held in a local: the delegate is marshalled as a function pointer,
        // and a temporary would be collectable while Windows is still calling
        // it. EnumWindows is synchronous, so it does not outlive this method.
        NativeMethods.WindowEnumProc collect = (window, _) =>
        {
            if (Wanted(window, ours, name) is { } shape)
            {
                var visible = shape.Intersect(desktop);

                if (!visible.IsEmpty)
                {
                    shapes.Add(visible);
                }
            }

            return true;
        };

        if (!NativeMethods.EnumWindows(collect, IntPtr.Zero))
        {
            // A partial walk is worse than none: the list is ordered, and a
            // list missing its front half answers with the wrong window rather
            // than with nothing.
            return WindowShapes.None;
        }

        return new WindowShapes(shapes);
    }

    /// <summary>
    /// The window's rectangle, or null when it is not something a person sees.
    /// </summary>
    private static CaptureRect? Wanted(IntPtr window, uint ours, StringBuilder name)
    {
        if (!NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
        {
            return null;
        }

        // Ours are out of the question. The overlay windows live in a pool and
        // are hidden rather than destroyed between captures, so the check above
        // already covers them - but a pinned screenshot is visible, and a pin
        // that the overlay treats as a window to select would be selecting
        // itself.
        NativeMethods.GetWindowThreadProcessId(window, out var owner);

        if (owner == ours)
        {
            return null;
        }

        // Composed but not on this virtual desktop, or a packaged application
        // Windows has suspended. IsWindowVisible says true for both.
        if (NativeMethods.DwmGetWindowAttribute(
                window, NativeMethods.DwmCloaked, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            return null;
        }

        if (SeenThrough(window))
        {
            return null;
        }

        name.Clear();

        if (NativeMethods.GetClassName(window, name, name.Capacity) > 0
            && Array.IndexOf(Desktop, name.ToString()) >= 0)
        {
            return null;
        }

        return Bounds(window);
    }

    /// <summary>
    /// The window is there but nothing of it can be seen or clicked.
    ///
    /// Two ways that happens, and both are real: a layered window given an
    /// alpha of zero, and a window the mouse is told to pass through. The
    /// NVIDIA overlay is the first - 2559x1440 of nothing across the primary
    /// monitor, found 11.09.2026 when a double click on bare desktop selected
    /// it instead of the screen. The second is what our own laser pane is, and
    /// a window a person cannot click is not a window they can point at.
    /// </summary>
    private static bool SeenThrough(IntPtr window)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(window, NativeMethods.GWL_EXSTYLE);

        if ((style & NativeMethods.WS_EX_TRANSPARENT) != 0)
        {
            return true;
        }

        if ((style & NativeMethods.WS_EX_LAYERED) == 0)
        {
            return false;
        }

        // Only meaningful when the window said it is using alpha at all: with
        // a colour key alone the byte is whatever was on the stack.
        return NativeMethods.GetLayeredWindowAttributes(window, out _, out var alpha, out var flags)
            && (flags & NativeMethods.LwaAlpha) != 0
            && alpha == 0;
    }

    /// <summary>
    /// What the window looks like, not what it occupies. The difference is the
    /// invisible resize border: GetWindowRect includes it, so a frame drawn to
    /// its answer has a strip of the window behind down each side.
    ///
    /// GetWindowRect is still the fallback - DWM has nothing to say about
    /// windows it does not compose, and an answer with a border is better than
    /// no answer.
    /// </summary>
    private static CaptureRect? Bounds(IntPtr window)
    {
        if (NativeMethods.DwmGetWindowAttribute(
                window,
                NativeMethods.DwmExtendedFrameBounds,
                out NativeMethods.Rect frame,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>()) == 0)
        {
            return Rectangle(frame);
        }

        return NativeMethods.GetWindowRect(window, out var box) ? Rectangle(box) : null;
    }

    private static CaptureRect? Rectangle(NativeMethods.Rect rect)
    {
        var shape = CaptureRect.FromPoints(rect.Left, rect.Top, rect.Right, rect.Bottom);

        return shape.IsEmpty ? null : shape;
    }
}
