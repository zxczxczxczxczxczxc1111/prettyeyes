using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using PrettyEyes.Core.Tools;

namespace PrettyEyes.App.Views;

/// <summary>
/// The single floating toolbar shown next to the selection. Owns no state
/// beyond which tool looks active - the session decides what the buttons mean.
/// </summary>
public partial class ToolbarView : UserControl
{
    private const string ActiveClass = "active";

    private ToolKind? _active;
    private bool _saveWithDialog;

    public ToolbarView()
    {
        InitializeComponent();

        // Right button on a tool opens its style card. Blur has none on
        // purpose: its strength is decided by the size of the region, and a
        // "weaker blur" option is a way to publish data you meant to hide.
        foreach (var (button, kind) in Buttons())
        {
            if (kind == ToolKind.Blur)
            {
                continue;
            }

            button.PointerPressed += (_, e) => OnToolPressed(kind, e);
        }

        BlurButton.Click += (_, _) => Pick(ToolKind.Blur);
        ArrowButton.Click += (_, _) => Pick(ToolKind.Arrow);
        LineButton.Click += (_, _) => Pick(ToolKind.Line);
        RectButton.Click += (_, _) => Pick(ToolKind.Rectangle);
        EmojiButton.Click += (_, _) => Pick(ToolKind.Emoji);
        UndoButton.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        CopyButton.Click += (_, _) => CopyClicked?.Invoke(this, EventArgs.Empty);
        // Shift on the save button means "ask me where", even when autosave is
        // on. The modifier is only available on the pointer event, not on Click.
        SaveButton.PointerPressed += (_, e) => _saveWithDialog = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        SaveButton.Click += (_, _) =>
        {
            SaveClicked?.Invoke(this, _saveWithDialog);
            _saveWithDialog = false;
        };
    }

    /// <summary>
    /// The chosen tool, or null when the active one was switched off and the
    /// pointer goes back to editing the selection.
    /// </summary>
    public event EventHandler<ToolKind?>? ToolPicked;

    public event EventHandler? UndoClicked;

    public event EventHandler? CopyClicked;

    /// <summary>True when the user asked for the file dialog on purpose.</summary>
    public event EventHandler<bool>? SaveClicked;

    /// <summary>Right click on a tool: its style card wants to open.</summary>
    public event EventHandler<ToolKind>? StyleRequested;

    /// <summary>Paints the dot that says what colour each tool will draw with.</summary>
    public void ShowStyles(ToolStyles styles)
    {
        foreach (var (dot, kind) in Dots())
        {
            dot.Fill = new SolidColorBrush(Color.FromUInt32(styles.For(kind).Color));
        }
    }

    private void OnToolPressed(ToolKind kind, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        // Handled here, or the overlay underneath treats it as a click on the
        // screen and starts a new selection.
        e.Handled = true;
        StyleRequested?.Invoke(this, kind);
    }

    /// <summary>
    /// Hides the tools that were turned off in the settings. Hidden and not
    /// disabled: a greyed-out row of buttons is the clutter the setting exists
    /// to get rid of.
    /// </summary>
    public void ShowTools(ToolVisibility tools)
    {
        foreach (var (button, kind) in Buttons())
        {
            button.IsVisible = tools.IsShown(kind);
        }
    }

    private IEnumerable<(Ellipse Dot, ToolKind Kind)> Dots()
    {
        yield return (ArrowDot, ToolKind.Arrow);
        yield return (LineDot, ToolKind.Line);
        yield return (RectDot, ToolKind.Rectangle);
    }

    /// <summary>Null means no tool is picked and the pointer edits the selection.</summary>
    public void SetActive(ToolKind? kind)
    {
        _active = kind;

        foreach (var (button, buttonKind) in Buttons())
        {
            button.Classes.Remove(ActiveClass);

            if (kind is not null && buttonKind == kind)
            {
                button.Classes.Add(ActiveClass);
            }
        }
    }

    /// <summary>Fades and lifts the card in; the transition lives in the XAML.</summary>
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

    /// <summary>
    /// A second click on the active tool switches it off: with a tool held the
    /// pointer draws, and there would be no way back to moving the frame.
    /// </summary>
    private void Pick(ToolKind kind)
    {
        var next = _active == kind ? (ToolKind?)null : kind;

        SetActive(next);
        ToolPicked?.Invoke(this, next);
    }

    private IEnumerable<(Button Button, ToolKind Kind)> Buttons()
    {
        yield return (BlurButton, ToolKind.Blur);
        yield return (ArrowButton, ToolKind.Arrow);
        yield return (LineButton, ToolKind.Line);
        yield return (RectButton, ToolKind.Rectangle);
        yield return (EmojiButton, ToolKind.Emoji);
    }

    /// <summary>Puts the chosen glyph on the emoji button.</summary>
    public void ShowGlyph(string code)
    {
        EmojiGlyph.Source = new Avalonia.Media.Imaging.Bitmap(
            Avalonia.Platform.AssetLoader.Open(
                new Uri($"avares://PrettyEyes.App/Assets/Emoji/{code}.png")));

        EmojiGlyph.IsVisible = true;
        EmojiOutline.IsVisible = false;
    }
}
