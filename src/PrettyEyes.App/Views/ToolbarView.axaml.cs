using Avalonia.Controls;
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

    public ToolbarView()
    {
        InitializeComponent();

        BlurButton.Click += (_, _) => Pick(ToolKind.Blur);
        ArrowButton.Click += (_, _) => Pick(ToolKind.Arrow);
        LineButton.Click += (_, _) => Pick(ToolKind.Line);
        RectButton.Click += (_, _) => Pick(ToolKind.Rectangle);
        UndoButton.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        CopyButton.Click += (_, _) => CopyClicked?.Invoke(this, EventArgs.Empty);
        SaveButton.Click += (_, _) => SaveClicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<ToolKind>? ToolPicked;

    public event EventHandler? UndoClicked;

    public event EventHandler? CopyClicked;

    public event EventHandler? SaveClicked;

    /// <summary>Null means no tool is picked and the pointer edits the selection.</summary>
    public void SetActive(ToolKind? kind)
    {
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

    private void Pick(ToolKind kind)
    {
        SetActive(kind);
        ToolPicked?.Invoke(this, kind);
    }

    private IEnumerable<(Button Button, ToolKind Kind)> Buttons()
    {
        yield return (BlurButton, ToolKind.Blur);
        yield return (ArrowButton, ToolKind.Arrow);
        yield return (LineButton, ToolKind.Line);
        yield return (RectButton, ToolKind.Rectangle);
    }
}
