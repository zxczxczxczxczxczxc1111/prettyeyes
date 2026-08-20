using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using PrettyEyes.Core.Tools;

namespace PrettyEyes.App.Views;

/// <summary>
/// Colour and thickness for one tool, opened with the right mouse button on its
/// toolbar button.
///
/// It stays open after a choice on purpose: colour and thickness are usually
/// changed together, and a card that closes on the first click makes the second
/// change a second trip.
/// </summary>
public partial class ToolStylePopup : UserControl
{
    private const string ActiveClass = "active";

    private ToolKind _kind = ToolKind.Arrow;
    private ToolStyle _style = ToolStyle.Default;

    public ToolStylePopup()
    {
        InitializeComponent();

        foreach (var colour in Palette.All)
        {
            Colors.Children.Add(NewSwatch(colour));
        }

        SmallStep.Click += (_, _) => Pick(_style with { Size = StrokeSize.Small });
        MediumStep.Click += (_, _) => Pick(_style with { Size = StrokeSize.Medium });
        LargeStep.Click += (_, _) => Pick(_style with { Size = StrokeSize.Large });
    }

    /// <summary>The tool and the style it should draw with from now on.</summary>
    public event EventHandler<(ToolKind Kind, ToolStyle Style)>? StyleChanged;

    public ToolKind Kind => _kind;

    public void Open(ToolKind kind, ToolStyle style)
    {
        _kind = kind;
        _style = style;

        Show();
        IsVisible = true;
        Card.Opacity = 1;
        Card.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    public void Close()
    {
        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("translateY(6px)");
        IsVisible = false;
    }

    private void Show()
    {
        foreach (var child in Colors.Children)
        {
            if (child is Button swatch && swatch.Tag is uint colour)
            {
                swatch.Content = NewFace(colour, selected: colour == _style.Color);
            }
        }

        foreach (var (button, size) in Steps())
        {
            button.Classes.Remove(ActiveClass);

            if (size == _style.Size)
            {
                button.Classes.Add(ActiveClass);
            }
        }
    }

    private void Pick(ToolStyle style)
    {
        _style = style;
        Show();
        StyleChanged?.Invoke(this, (_kind, style));
    }

    private Button NewSwatch(uint colour)
    {
        var button = new Button
        {
            Tag = colour,
            Content = NewFace(colour, selected: false),
        };

        button.Classes.Add("swatch");
        button.Click += (_, _) => Pick(_style with { Color = colour });

        return button;
    }

    /// <summary>
    /// The circle itself, with a tick when it is the chosen one. Drawn rather
    /// than styled because the tick has to sit on top of an arbitrary colour.
    /// </summary>
    private static Control NewFace(uint colour, bool selected)
    {
        var brush = new SolidColorBrush(Color.FromUInt32(colour));
        var panel = new Panel
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        panel.Children.Add(new Ellipse
        {
            Fill = brush,
            Width = 18,
            Height = 18,
        });

        if (selected)
        {
            // Dark tick on light colours, light tick on dark ones: the palette
            // runs from carmine to white and no single ink works on both.
            var ink = Luminance(colour) > 0.6 ? Brushes.Black : Brushes.White;

            panel.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = Avalonia.Media.Geometry.Parse("M4,9 L7.5,12.5 L14,5.5"),
                Stroke = ink,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
            });
        }

        return panel;
    }

    private static double Luminance(uint colour)
    {
        var r = ((colour >> 16) & 0xFF) / 255.0;
        var g = ((colour >> 8) & 0xFF) / 255.0;
        var b = (colour & 0xFF) / 255.0;

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private IEnumerable<(Button Button, StrokeSize Size)> Steps()
    {
        yield return (SmallStep, StrokeSize.Small);
        yield return (MediumStep, StrokeSize.Medium);
        yield return (LargeStep, StrokeSize.Large);
    }
}
