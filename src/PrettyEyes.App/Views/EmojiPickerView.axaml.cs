using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;

namespace PrettyEyes.App.Views;

/// <summary>
/// The forty bundled glyphs, plus the ones this user reaches for.
/// </summary>
public partial class EmojiPickerView : UserControl
{
    private const int RecentCount = 8;

    private readonly List<string> _recent = [];

    public EmojiPickerView()
    {
        InitializeComponent();

        foreach (var code in Services.EmojiAtlas.All)
        {
            Grid.Children.Add(NewButton(code));
        }
    }

    /// <summary>The glyph the user wants to stamp.</summary>
    public event EventHandler<string>? Picked;

    /// <summary>Most recent first, for the settings to keep.</summary>
    public IReadOnlyList<string> Recent => _recent;

    public void Restore(IReadOnlyList<string> recent)
    {
        _recent.Clear();
        _recent.AddRange(recent.Where(Services.EmojiAtlas.All.Contains).Take(RecentCount));
        ShowRecent();
    }

    public void Open()
    {
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

    private void Pick(string code)
    {
        _recent.Remove(code);
        _recent.Insert(0, code);

        while (_recent.Count > RecentCount)
        {
            _recent.RemoveAt(_recent.Count - 1);
        }

        ShowRecent();
        Picked?.Invoke(this, code);
    }

    private void ShowRecent()
    {
        RecentRow.Children.Clear();

        foreach (var code in _recent)
        {
            RecentRow.Children.Add(NewButton(code));
        }
    }

    private Button NewButton(string code)
    {
        var button = new Button
        {
            Content = new Image
            {
                Width = 22,
                Height = 22,
                Source = new Bitmap(AssetLoader.Open(
                    new Uri($"avares://PrettyEyes.App/Assets/Emoji/{code}.png"))),
            },
        };

        button.Classes.Add("glyph");
        button.Click += (_, _) => Pick(code);

        return button;
    }
}
