using Avalonia.Controls;
using PrettyEyes.Core.Geometry;

namespace PrettyEyes.App.Views;

/// <summary>
/// Shows the selection size. The numbers are physical pixels: that is the size
/// of the image the user ends up with, logical pixels would be a lie.
/// </summary>
public partial class SizeChip : UserControl
{
    public SizeChip() => InitializeComponent();

    public void Update(CaptureRect selection) =>
        Label.Text = $"{selection.Width} x {selection.Height}";

    public void FadeIn() => Card.Opacity = 1;

    public void FadeOut() => Card.Opacity = 0;
}
