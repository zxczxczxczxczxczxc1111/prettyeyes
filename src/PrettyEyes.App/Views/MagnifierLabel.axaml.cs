using Avalonia.Controls;
using Avalonia.Media;
using PrettyEyes.Core.Geometry;
using SkiaSharp;

namespace PrettyEyes.App.Views;

/// <summary>
/// The line under the magnifier: where the cursor is and what colour is under
/// it, or the size of the selection once one is being dragged.
///
/// A control rather than something painted into the canvas: the canvas draws in
/// physical pixels with the DPI scale undone, and text drawn there would be
/// unreadable at 150%. There is also no font loaded on that side.
/// </summary>
public partial class MagnifierLabel : UserControl
{
    public MagnifierLabel() => InitializeComponent();

    /// <summary>Before a selection exists: the pixel and its colour.</summary>
    public void ShowPixel(int x, int y, SKColor? colour)
    {
        Position.Text = $"{x}, {y}";

        if (colour is { } value)
        {
            Value.Text = $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
            Swatch.Background = new SolidColorBrush(Color.FromRgb(value.Red, value.Green, value.Blue));
            Swatch.IsVisible = true;
        }
        else
        {
            // Off the captured frame: there is no colour to name.
            Value.Text = "-";
            Swatch.IsVisible = false;
        }
    }

    /// <summary>While a selection is being dragged its size matters more.</summary>
    public void ShowSize(CaptureRect selection)
    {
        Position.Text = $"{selection.Width} x {selection.Height}";
        Value.Text = string.Empty;
        Swatch.IsVisible = false;
    }

    /// <summary>Says the colour went to the clipboard, for a moment.</summary>
    public void ShowCopied(SKColor colour)
    {
        Position.Text = $"#{colour.Red:X2}{colour.Green:X2}{colour.Blue:X2}";
        Value.Text = "скопирован";
        Swatch.Background = new SolidColorBrush(Color.FromRgb(colour.Red, colour.Green, colour.Blue));
        Swatch.IsVisible = true;
    }

    public void Show() => Card.Opacity = 1;

    public void Hide() => Card.Opacity = 0;
}
