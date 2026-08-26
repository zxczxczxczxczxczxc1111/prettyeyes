using SkiaSharp;

namespace PrettyEyes.Core.Geometry;

/// <summary>
/// The line under the magnifier, as a value.
///
/// A value rather than three assignments on a control: the mouse reports the
/// same physical pixel over and over, and every assignment to a TextBlock costs
/// a layout pass whether or not the string differs. Comparing one struct is
/// what lets the label skip the work.
/// </summary>
public readonly record struct MagnifierReadout(string Position, string Value, SKColor? Swatch)
{
    /// <summary>Before a selection exists: the pixel and its colour.</summary>
    public static MagnifierReadout ForPixel(int x, int y, SKColor? colour) =>
        colour is { } value
            ? new MagnifierReadout($"{x}, {y}", Hex(value), value)

            // Off the captured frame: there is no colour to name.
            : new MagnifierReadout($"{x}, {y}", "-", null);

    /// <summary>While a selection is being dragged its size matters more.</summary>
    public static MagnifierReadout ForSize(CaptureRect selection) =>
        new($"{selection.Width} x {selection.Height}", string.Empty, null);

    /// <summary>Says the colour went to the clipboard, for a moment.</summary>
    public static MagnifierReadout Copied(SKColor colour) =>
        new(Hex(colour), "скопирован", colour);

    private static string Hex(SKColor colour) =>
        $"#{colour.Red:X2}{colour.Green:X2}{colour.Blue:X2}";
}
