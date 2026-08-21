using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PrettyEyes.Core.Geometry;
using PrettyEyes.Core.Model;
using PrettyEyes.Core.Rendering;
using SkiaSharp;

namespace PrettyEyes.App.Views;

/// <summary>
/// Padding, backdrop, rounding and the flourishes around an exported
/// screenshot. Behind the export icon now: it was the largest section on the
/// page, and it is the one people open least often.
/// </summary>
public partial class ExportSettingsView : UserControl
{
    private ExportStyle _export = ExportStyle.None;

    private bool _loading;

    public ExportSettingsView()
    {
        InitializeComponent();

        ExportAdvanced.Click += (_, _) => ShowAdvanced(!ExportOptions.IsVisible);
        ExportShadow.IsCheckedChanged += (_, _) => Apply(_export with { Shadow = ExportShadow.IsChecked == true });
        ExportGrain.IsCheckedChanged += (_, _) => Apply(_export with { Grain = ExportGrain.IsChecked == true });
        ExportSheen.IsCheckedChanged += (_, _) => Apply(_export with { Sheen = ExportSheen.IsChecked == true });
    }

    /// <summary>The style changed. The window stores it and repaints its icon.</summary>
    public event EventHandler<ExportStyle>? Changed;

    public void Load(ExportStyle style)
    {
        _loading = true;
        _export = style;
        ShowState(_export.Enabled);
        BuildRows();
        ShowPreview();
        _loading = false;
    }

    /// <summary>
    /// The three ways a screenshot can leave the application.
    ///
    /// Nothing is stored about which one is picked: a preset is highlighted
    /// when the current style equals it, and `ExportStyle` is a record, so that
    /// comparison is free and cannot drift from the truth. Change a parameter
    /// by hand and no preset lights up, which is exactly what happened.
    /// </summary>
    private static readonly (string Label, ExportStyle Style)[] Presets =
    [
        ("нет", ExportStyle.None),
        ("карточка", ExportStyle.Card),
        ("на белом", ExportStyle.Sheet),
    ];

    /// <summary>
    /// Rendered once, when the window opens. A preset does not change, so
    /// redrawing its picture on every click would be work for an identical
    /// result - and with the aura that work includes a blur.
    /// </summary>
    private readonly Dictionary<string, Bitmap> _presetPreviews = [];

    private void BuildPresetRow()
    {
        PresetRow.Children.Clear();

        foreach (var (label, style) in Presets)
        {
            var chosen = style.Enabled
                ? _export == style
                : !_export.Enabled;

            if (!_presetPreviews.TryGetValue(label, out var preview))
            {
                preview = RenderSample(style);
                _presetPreviews[label] = preview;
            }

            PresetRow.Children.Add(NewPreset(label, preview, chosen, () => Apply(style)));
        }

        // Shown only to whoever went looking for a transparent background in
        // the parameters: from the presets it is not offered at all, because
        // the clipboard cannot deliver it.
        PresetHint.IsVisible = _export is { Enabled: true, Background: ExportBackground.Transparent };
        PresetHint.Text = "Прозрачность попадёт только в файл. Буфер обмена в Windows "
            + "не умеет её переносить, поэтому при вставке фон будет белым.";
    }

    private void ShowAdvanced(bool shown)
    {
        ExportOptions.IsVisible = shown;
        ExportAdvanced.Content = shown ? "Свернуть" : "Настроить";
    }

    /// <summary>
    /// Padding, backdrop and rounding as rows of small buttons. Built in code
    /// because they are three of the same thing, and three copies of the same
    /// markup would drift apart.
    /// </summary>
    private void BuildRows()
    {
        BuildPresetRow();
        PaddingRow.Children.Clear();
        BackgroundRow.Children.Clear();
        RadiusRow.Children.Clear();

        foreach (var (label, value) in new (string, int)[] { ("нет", 0), ("24", 24), ("48", 48), ("72", 72) })
        {
            PaddingRow.Children.Add(NewChoice(label, _export.Padding == value, () =>
                Apply(_export with { Padding = value })));
        }

        foreach (var (label, value) in new (string, ExportBackground)[]
        {
            ("чёрный", ExportBackground.Black),
            ("градиент", ExportBackground.Gradient),
            ("дымка", ExportBackground.Aura),
            ("белый", ExportBackground.White),
            ("прозрачный", ExportBackground.Transparent),
        })
        {
            BackgroundRow.Children.Add(NewChoice(label, _export.Background == value, () =>
                Apply(_export with { Background = value })));
        }

        foreach (var value in new[] { 0, 8, 16 })
        {
            RadiusRow.Children.Add(NewChoice(value == 0 ? "нет" : value.ToString(), _export.CornerRadius == value, () =>
                Apply(_export with { CornerRadius = value })));
        }

        // A shadow with no padding falls outside the picture, so the switch
        // says so instead of doing nothing.
        ExportShadow.IsEnabled = _export.ShadowAllowed;
        ExportShadow.IsChecked = _export.Shadow && _export.ShadowAllowed;

        // Grain is backdrop work, and two of the backdrops have nothing for it
        // to work on. The switch says so instead of doing nothing.
        ExportGrain.IsEnabled = _export.GrainApplies;
        ExportGrain.IsChecked = _export.GrainAllowed;
        ExportSheen.IsChecked = _export.Sheen;
    }

    /// <summary>
    /// A preset button: the picture it produces, with its name under it.
    /// Choosing between three results, rather than between three words.
    /// </summary>
    private Button NewPreset(string label, Bitmap preview, bool active, Action pick)
    {
        var stack = new StackPanel { Spacing = 6 };

        stack.Children.Add(new Border
        {
            CornerRadius = new Avalonia.CornerRadius(4),
            ClipToBounds = true,
            Child = new Image { Source = preview, Width = 96, Height = 68, Stretch = Stretch.UniformToFill },
        });

        stack.Children.Add(new TextBlock
        {
            Text = label,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontSize = 11,
        });

        var button = new Button { Content = stack, Padding = new Avalonia.Thickness(6) };

        button.Classes.Add("choice");

        if (active)
        {
            button.Classes.Add("active");
        }

        button.Click += (_, _) => pick();

        return button;
    }

    private Button NewChoice(string label, bool active, Action pick)
    {
        var button = new Button { Content = label };

        button.Classes.Add("choice");

        if (active)
        {
            button.Classes.Add("active");
        }

        button.Click += (_, _) => pick();

        return button;
    }

    private void Apply(ExportStyle style)
    {
        if (_loading)
        {
            return;
        }

        _export = style with { Shadow = style.Shadow && style.ShadowAllowed };

        ShowState(_export.Enabled);
        BuildRows();
        ShowPreview();

        Changed?.Invoke(this, _export);
    }

    /// <summary>
    /// Deliberately does not redraw the preview. It used to, and since the
    /// caller draws it a line later, every change of a parameter rendered the
    /// sample twice - which with the aura means blurring it twice.
    /// </summary>
    private void ShowState(bool enabled)
    {
        ExportOptions.IsEnabled = enabled;
        ExportOptions.Opacity = enabled ? 1 : 0.4;
    }

    /// <summary>
    /// A sample screenshot run through the real renderer. Anything else would
    /// be a drawing of what the export might look like.
    /// </summary>
    private void ShowPreview() => ExportPreview.Source = RenderSample(_export);

    /// <summary>
    /// The stand-in screenshot every preview is built from.
    ///
    /// It carries colour on purpose. The old sample was grey lines on grey, and
    /// on a backdrop drawn from the picture's own colours that showed nothing
    /// at all: a preview of the aura has to have something to be an aura of.
    /// The colour lives inside the sample, not in the interface around it.
    /// </summary>
    private static SKImage SampleShot()
    {
        using var surface = SKSurface.Create(new SKImageInfo(320, 200));
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(0x14, 0x14, 0x18));

        using var line = new SKPaint { Color = new SKColor(0x33, 0x33, 0x3A), StrokeWidth = 8 };

        for (var y = 40; y < 200; y += 32)
        {
            canvas.DrawLine(24, y, y < 120 ? 296 : 200, y, line);
        }

        using var accent = new SKPaint { Color = new SKColor(0xB0, 0x10, 0x30), StrokeWidth = 8 };
        canvas.DrawLine(24, 24, 120, 24, accent);

        var dots = new[]
        {
            new SKColor(0x8E, 0x4E, 0xC6),
            new SKColor(0x00, 0x91, 0xFF),
            new SKColor(0x30, 0xA4, 0x6C),
        };

        for (var i = 0; i < dots.Length; i++)
        {
            using var dot = new SKPaint { Color = dots[i], IsAntialias = true };
            canvas.DrawCircle(240 + (i * 26), 150, 9, dot);
        }

        return surface.Snapshot();
    }

    /// <summary>The sample under one style, as a bitmap the interface can show.</summary>
    private static Bitmap RenderSample(ExportStyle style)
    {
        using var sample = SampleShot();
        using var document = new Document(sample, new CaptureRect(0, 0, 320, 200))
        {
            Selection = new CaptureRect(0, 0, 320, 200),
        };

        using var rendered = DocumentRenderer.Render(document, style);
        using var data = rendered.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = new MemoryStream(data.ToArray());

        return new Bitmap(stream);
    }

}
