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
    public void Update(CaptureRect selection, ExportStyle? style = null)
    {
        var fitted = (style ?? ExportStyle.None).FitTo(selection.Width, selection.Height);

        Label.Text = fitted.Enabled && fitted.Padding > 0
            ? $"{selection.Width} x {selection.Height} -> {selection.Width + (fitted.Padding * 2)} x {selection.Height + (fitted.Padding * 2)}"
            : $"{selection.Width} x {selection.Height}";
    }

    public void FadeIn()
    {
        Card.Opacity = 1;
        Card.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    public void FadeOut()
    {
        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("translateY(8px)");
    }
}
