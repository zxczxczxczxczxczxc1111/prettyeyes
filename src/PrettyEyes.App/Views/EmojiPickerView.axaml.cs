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

    /// <summary>
    /// Decoded once for the whole process.
    ///
    /// It used to be one decode per glyph per window: the picker lives in every
    /// overlay window and in every pinned one, so two monitors and a pin came
    /// to a hundred and twenty bitmaps of the same forty pictures. The recent
    /// row rebuilt eight more on every pick and let the old ones go without
    /// disposing them.
    ///
    /// A plain Dictionary because this is only ever touched from the UI thread.
    /// Never emptied: forty small pictures for the life of a process is the
    /// point, not a leak.
    /// </summary>
    private static readonly Dictionary<string, Bitmap> Glyphs = [];

    private readonly List<string> _recent = [];

    private bool _filled;

    public EmojiPickerView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Builds the grid, once, the first time the card is opened.
    ///
    /// Not in the constructor: the card lives in every overlay window and in
    /// every pinned one, all of which are built at start-up, and filling it
    /// there would decode the whole set before anyone has asked for an emoji.
    /// </summary>
    private void Fill()
    {
        if (_filled)
        {
            return;
        }

        _filled = true;

        foreach (var code in Services.EmojiAtlas.All)
        {
            Grid.Children.Add(NewButton(code));
        }

        ShowRecent();
    }

    /// <summary>The glyph the user wants to stamp.</summary>
    public event EventHandler<string>? Picked;

    /// <summary>Most recent first, for the settings to keep.</summary>
    public IReadOnlyList<string> Recent => _recent;

    public void Restore(IReadOnlyList<string> recent)
    {
        _recent.Clear();
        _recent.AddRange(recent.Where(Services.EmojiAtlas.All.Contains).Take(RecentCount));

        // Drawn when the card opens. Restore runs at the start of every
        // capture, and a row of pictures nobody has asked to see is work done
        // while the screen is frozen.
        if (_filled)
        {
            ShowRecent();
        }
    }

    public void Open()
    {
        Fill();

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


    /// <summary>
    /// The picture for a glyph, shared by every row and every window.
    ///
    /// From the small set rather than the one the stamp uses. It is shown at
    /// 22 logical pixels, which is 55 physical at the 250% Windows offers, and
    /// a 64-pixel file covers that; the 256-pixel one the stamp reads would be
    /// sixteen times the memory for a picture nobody sees at that size. One
    /// shared bitmap cannot be decoded per monitor scaling anyway.
    /// </summary>
    internal static Bitmap For(string code)
    {
        if (Glyphs.TryGetValue(code, out var cached))
        {
            return cached;
        }

        using var stream = AssetLoader.Open(
            new Uri($"avares://PrettyEyes.App/Assets/Emoji/small/{code}.png"));

        var glyph = new Bitmap(stream);
        Glyphs[code] = glyph;

        return glyph;
    }

    private Button NewButton(string code)
    {
        var button = new Button
        {
            Content = new Image
            {
                Width = 22,
                Height = 22,
                Source = For(code),
            },
        };

        button.Classes.Add("glyph");
        button.Click += (_, _) => Pick(code);

        return button;
    }
}
