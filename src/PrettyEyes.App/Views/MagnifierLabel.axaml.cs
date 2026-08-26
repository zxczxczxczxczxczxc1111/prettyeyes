using Avalonia.Controls;
using Avalonia.Media;
using PrettyEyes.Core.Geometry;

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
    // Built once and mutated. A fresh brush per pointer move is an
    // AvaloniaObject per pointer move, with all the property-change plumbing
    // that comes with one, for a colour that usually did not change.
    private readonly SolidColorBrush _swatch = new();

    private MagnifierReadout _shown;

    public MagnifierLabel()
    {
        InitializeComponent();
        Swatch.Background = _swatch;
    }

    /// <summary>
    /// Puts a reading on the plate. Answers whether anything changed, which is
    /// how the caller knows the plate needs measuring again.
    /// </summary>
    public bool Update(MagnifierReadout readout)
    {
        if (_shown == readout)
        {
            return false;
        }

        Position.Text = readout.Position;
        Value.Text = readout.Value;

        if (readout.Swatch is { } colour)
        {
            _swatch.Color = Color.FromRgb(colour.Red, colour.Green, colour.Blue);
            Swatch.IsVisible = true;
        }
        else
        {
            Swatch.IsVisible = false;
        }

        _shown = readout;

        return true;
    }

    public void Show() => Card.Opacity = 1;

    public void Hide()
    {
        Card.Opacity = 0;

        // Forgotten on the way out: coming back to the same pixel must still
        // repaint the plate, and must still measure it, or it returns holding
        // the width of whatever it last showed.
        _shown = default;
    }
}
