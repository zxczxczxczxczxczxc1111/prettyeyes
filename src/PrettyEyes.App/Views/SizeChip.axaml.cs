using Avalonia.Controls;
using Avalonia.Media.Transformation;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Rendering;

namespace PrettyEyes.App.Views;

/// <summary>
/// Shows the selection size. The numbers are physical pixels: that is the size
/// of the image the user ends up with, logical pixels would be a lie.
/// </summary>
public partial class SizeChip : UserControl
{
    public SizeChip() => InitializeComponent();

    /// <summary>
    /// The selection, and where the export style is on, what the file will
    /// actually be: promising 1200 x 800 and writing 1296 x 896 is a small lie
    /// that costs somebody a re-crop.
    /// </summary>
    /// <returns>
    /// Whether the text changed. A drag along one axis keeps the other number
    /// the same, and a drag that stays inside one pixel step changes neither:
    /// the caller uses this to skip the measure pass.
    /// </returns>
    public bool Update(CaptureRect selection, ExportStyle? style = null)
    {
        var fitted = (style ?? ExportStyle.None).FitTo(selection.Width, selection.Height);

        var text = fitted.Enabled && fitted.Padding > 0
            ? $"{selection.Width} x {selection.Height} -> {selection.Width + (fitted.Padding * 2)} x {selection.Height + (fitted.Padding * 2)}"
            : $"{selection.Width} x {selection.Height}";

        if (Label.Text == text)
        {
            return false;
        }

        Label.Text = text;
        return true;
    }

    public void FadeIn()
    {
        // Same as the toolbar: the chip is in after the first frame of a drag,
        // and every later call would re-parse the string for nothing.
        if (Card.Opacity >= 1)
        {
            return;
        }

        Card.Opacity = 1;
        Card.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    public void FadeOut()
    {
        if (Card.Opacity <= 0)
        {
            return;
        }

        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("translateY(8px)");
    }
}
