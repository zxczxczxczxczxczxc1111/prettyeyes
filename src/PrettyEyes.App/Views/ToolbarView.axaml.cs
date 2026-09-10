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
        PencilButton.Click += (_, _) => Pick(ToolKind.Pencil);
        MarkerButton.Click += (_, _) => Pick(ToolKind.Marker);
        EmojiButton.Click += (_, _) => Pick(ToolKind.Emoji);
        TextButton.Click += (_, _) => Pick(ToolKind.Text);
        LaserButton.Click += (_, _) => LaserClicked?.Invoke(this, EventArgs.Empty);
        PinButton.Click += (_, _) => PinClicked?.Invoke(this, EventArgs.Empty);
        UndoButton.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        CopyButton.Click += (_, _) => CopyClicked?.Invoke(this, EventArgs.Empty);
        // Shift on the save button means "ask me where", even when autosave is
        // on. The modifier is only available on the pointer event, not on Click.
        SaveButton.Click += (_, _) => SaveClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The chosen tool, or null when the active one was switched off and the
    /// pointer goes back to editing the selection.
    /// </summary>
    public event EventHandler<ToolKind?>? ToolPicked;

    /// <summary>The pointer was switched on or off. Which of the two is the session's business.</summary>
    public event EventHandler? LaserClicked;

    /// <summary>Nail the selection above every window and leave the overlay up.</summary>
    public event EventHandler? PinClicked;

    public event EventHandler? UndoClicked;

    public event EventHandler? CopyClicked;

    /// <summary>Save to a file, through the dialog.</summary>
    public event EventHandler? SaveClicked;

    /// <summary>Right click on a tool: its style card wants to open.</summary>
    public event EventHandler<ToolKind>? StyleRequested;

    /// <summary>
    /// Whether the pin button is offered at all. A pinned window is already
    /// pinned, and the button there would nail a copy of itself to the same
    /// spot.
    /// </summary>
    public bool CanPin
    {
        set => PinButton.IsVisible = value;
    }

    /// <summary>Whether the pointer is offered at all. Switched off in the settings like the pin.</summary>
    public bool CanLase
    {
        set => LaserButton.IsVisible = value;
    }

    /// <summary>
    /// Lit while the pointer is armed. Its own switch rather than SetActive:
    /// SetActive speaks in ToolKind, and the pointer is not one.
    /// </summary>
    public void SetLaserActive(bool active)
    {
        LaserButton.Classes.Remove(ActiveClass);

        if (active)
        {
            LaserButton.Classes.Add(ActiveClass);
        }
    }

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
        yield return (PencilDot, ToolKind.Pencil);
        yield return (MarkerDot, ToolKind.Marker);
        yield return (TextDot, ToolKind.Text);
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
        // Called from every step of a drag, and the card is already in by the
        // second one. Without this the string below is parsed sixty times a
        // second to arrive at the transform that is already there.
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

    /// <summary>
    /// A second click on the active tool switches it off: with a tool held the
    /// pointer draws, and there would be no way back to moving the frame.
    /// </summary>
    private void Pick(ToolKind kind)
    {
        // No SetActive here on purpose. The session owns the truth; the toolbar
        // is a display. It used to be both, and the two drifted apart every
        // time the pooled window outlived the session that set it.
        ToolPicked?.Invoke(this, ToolSelection.Next(_active, kind));
    }

    private IEnumerable<(Button Button, ToolKind Kind)> Buttons()
    {
        yield return (BlurButton, ToolKind.Blur);
        yield return (ArrowButton, ToolKind.Arrow);
        yield return (LineButton, ToolKind.Line);
        yield return (EmojiButton, ToolKind.Emoji);
        yield return (RectButton, ToolKind.Rectangle);
        yield return (PencilButton, ToolKind.Pencil);
        yield return (MarkerButton, ToolKind.Marker);
        yield return (TextButton, ToolKind.Text);
    }

    /// <summary>Puts the chosen glyph on the emoji button.</summary>
    public void ShowGlyph(string code)
    {
        // The picker's cache, not a decode of its own. This runs on every glyph
        // pick and once per window, and it overwrote Source each time, dropping
        // the previous picture without disposing it.
        EmojiGlyph.Source = EmojiPickerView.For(code);

        EmojiGlyph.IsVisible = true;
        EmojiOutline.IsVisible = false;
    }
}
