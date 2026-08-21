using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using PrettyEyes.Core.Annotations;
using PrettyEyes.Core.Tools;
using SkiaSharp;

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

    /// <summary>Showing a style rather than reacting to a choice.</summary>
    private bool _loading;

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

        // Null first, then whatever this machine has, alphabetically. The list
        // is no longer curated: a dropdown can hold three hundred families
        // where a column of buttons could not.
        Fonts.ItemsSource = Families;
        Fonts.SelectionChanged += (_, _) =>
        {
            if (!_loading && Fonts.SelectedItem is string chosen)
            {
                Pick(_style with { FontFamily = chosen == SystemFont ? null : chosen });
            }
        };

        SmallerType.Click += (_, _) => Resize(-TextAnnotation.SizeStep);
        BiggerType.Click += (_, _) => Resize(TextAnnotation.SizeStep);
        PlateBackdrop.Click += (_, _) => Pick(_style with { TextBackdrop = TextBackdrop.Plate });
        OutlineBackdrop.Click += (_, _) => Pick(_style with { TextBackdrop = TextBackdrop.Outline });
    }

    /// <summary>What the first entry says. Stored as null, shown as a word.</summary>
    private const string SystemFont = "Системный";

    /// <summary>
    /// Only the families this machine actually has. Offering one that is not
    /// installed would silently draw something else.
    /// </summary>
    private static readonly HashSet<string> Installed =
        [.. SKFontManager.Default.GetFontFamilies()];

    /// <summary>
    /// Everything installed, with the system font first. Sorted, because the
    /// font manager hands them back in whatever order it found them and a list
    /// of three hundred unsorted names is a list nobody can use.
    /// </summary>
    private static readonly string[] Families =
        [SystemFont, .. Installed.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)];

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

        var text = _kind == ToolKind.Text;

        StrokeRow.IsVisible = !text;
        TextRow.IsVisible = text;

        foreach (var (button, size) in Steps())
        {
            button.Classes.Remove(ActiveClass);

            if (size == _style.Size)
            {
                button.Classes.Add(ActiveClass);
            }
        }

        if (!text)
        {
            return;
        }

        TypeSize.Text = _style.FontSize.ToString(CultureInfo.InvariantCulture);

        // Setting the selection raises SelectionChanged, and answering that
        // would store the style we are only displaying.
        _loading = true;
        Fonts.SelectedItem = _style.FontFamily ?? SystemFont;
        _loading = false;

        Mark(PlateBackdrop, _style.TextBackdrop == TextBackdrop.Plate);
        Mark(OutlineBackdrop, _style.TextBackdrop == TextBackdrop.Outline);

        // Said out loud rather than silently substituted. A settings file can
        // arrive from another machine, and "why is the font wrong" has no other
        // answer anywhere in the interface.
        var missing = _style.FontFamily is { } family && !Installed.Contains(family);

        FontMissing.IsVisible = missing;
        FontMissing.Text = missing ? $"Шрифта «{_style.FontFamily}» здесь нет, взят системный" : string.Empty;
    }

    private static void Mark(Button button, bool active)
    {
        button.Classes.Remove(ActiveClass);

        if (active)
        {
            button.Classes.Add(ActiveClass);
        }
    }

    /// <summary>
    /// The size for the next label. Labels already on the screenshot keep
    /// theirs: the wheel over one of those is what changes that one, and this
    /// card has no idea which one the user means.
    /// </summary>
    private void Resize(int step) =>
        Pick(_style with
        {
            FontSize = Math.Clamp(_style.FontSize + step, ToolStyle.MinFontSize, ToolStyle.MaxFontSize),
        });

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
