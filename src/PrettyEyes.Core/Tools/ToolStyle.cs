namespace PrettyEyes.Core.Tools;

/// <summary>
/// How thick a drawn shape is, in three steps.
///
/// Kept for the settings file and nothing else. The card asks for a number of
/// pixels now and Width is what anything drawing reads; this is written
/// alongside it as the nearest step, so a build that predates Width still finds
/// a thickness instead of falling back to the thinnest line there is. The value
/// carried in memory means nothing - it is recomputed on the way to the file.
/// </summary>
public enum StrokeSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// How a label stays readable over whatever it landed on. Both answers are
/// needed: a plate is calmer on a busy screenshot, an outline hides less of it.
/// </summary>
public enum TextBackdrop
{
    Plate,
    Outline,
}

/// <summary>
/// What a tool draws with. Blur is deliberately absent from this: its radius
/// comes from the size of the region and is always strong enough, because weak
/// blur is reversible and the tool exists to hide data.
///
/// The text fields sit at the end with defaults, so every existing call site
/// and every settings file written before them still means what it meant.
/// FontFamily is null for "whatever this machine calls its interface font":
/// storing the resolved name would pin a screenshot to the machine it was
/// taken on.
///
/// FreehandArrow is a mode of the arrow rather than a tool of its own: the
/// toolbar stays eight buttons wide, the visibility settings keep meaning what
/// they meant, and a file written before this field still opens as a straight
/// arrow, which is what every version so far drew.
/// </summary>
public sealed record ToolStyle(
    uint Color,
    StrokeSize Size,
    string? FontFamily = null,
    int FontSize = ToolStyle.DefaultFontSize,
    TextBackdrop TextBackdrop = TextBackdrop.Plate,
    int TextPadding = ToolStyle.DefaultTextPadding,
    bool FreehandArrow = false,
    int Width = 0)
{
    /// <summary>Where a label starts before the wheel touches it.</summary>
    public const int DefaultFontSize = 18;

    /// <summary>Breathing room between the glyphs and the edge of the plate.</summary>
    public const int DefaultTextPadding = 4;

    /// <summary>Smallest type that is still worth reading on a screenshot.</summary>
    public const int MinFontSize = 8;

    /// <summary>Largest type. Past this a label is a poster, not a note.</summary>
    public const int MaxFontSize = 200;

    /// <summary>Thinnest line worth drawing.</summary>
    public const int MinWidth = 1;

    /// <summary>
    /// Thick enough to cover a face in one pass, which is what the widest
    /// setting is for. Past this a stroke hides the screenshot rather than
    /// marking it.
    /// </summary>
    public const int MaxWidth = 60;

    /// <summary>What the middle preset drew, and what a new style starts at.</summary>
    public const int DefaultWidth = 3;

    /// <summary>Carmine at the middle width, which is what every version so far drew.</summary>
    public static ToolStyle Default => new(Palette.Carmine, StrokeSize.Medium, Width: DefaultWidth);

    /// <summary>
    /// What a tool draws with before anyone has an opinion. Only the
    /// highlighter differs: carmine multiplied into a page is a wound, and the
    /// colour everyone reaches for on paper is yellow.
    /// </summary>
    public static ToolStyle DefaultFor(ToolKind kind) => kind switch
    {
        // Wide by itself now. This used to be a hidden multiplier of four
        // applied by the annotation, which made the number on the card a lie.
        // The preset passed in means nothing - it is rewritten on the way to
        // the file - so it is spelled default rather than pretending to a value.
        ToolKind.Marker => new ToolStyle(Palette.Yellow, default).WithWidth(12),

        // White on a plate, because carmine text on a dark screenshot is a
        // decoration rather than a message. Task 13 wires the popup to it.
        ToolKind.Text => new(Palette.White, StrokeSize.Medium, Width: DefaultWidth),

        // Freehand out of the box: it is the arrow people reach for, and the
        // straight one is a click away in the card.
        ToolKind.Arrow => Default with { FreehandArrow = true },
        _ => Default,
    };

    /// <summary>
    /// Stroke width in physical pixels.
    ///
    /// Zero means a settings file older than this field. Normalize turns those
    /// into real numbers because only it knows which tool a style belongs to,
    /// and the marker needs more than the others; this fallback is for a style
    /// that never went through it.
    /// </summary>
    public float StrokeWidth => Width > 0 ? Width : DefaultWidth;

    /// <summary>
    /// A new thickness, pulled into range.
    ///
    /// Size is deliberately left alone. It is written at the file boundary,
    /// where the tool is known - the released build multiplies the marker's
    /// preset by four on its own, so a preset computed here would be four times
    /// wrong for that one tool, and wrong in a direction nobody would notice
    /// until they went back a version.
    /// </summary>
    public ToolStyle WithWidth(int pixels) =>
        this with { Width = Math.Clamp(pixels, MinWidth, MaxWidth) };

    /// <summary>
    /// Which of the three old steps a width is closest to. For the file only:
    /// nothing in this build draws by preset any more.
    /// </summary>
    public static StrokeSize NearestSize(int width) => width switch
    {
        <= 2 => StrokeSize.Small,
        <= 3 => StrokeSize.Medium,
        _ => StrokeSize.Large,
    };
}

/// <summary>
/// The eight colours a shape can be drawn in. These are content colours, not
/// interface ones: the interface stays monochrome, what lands on the screenshot
/// does not have to.
/// </summary>
public static class Palette
{
    public const uint Carmine = 0xFFB01030;
    public const uint Red = 0xFFE5484D;
    public const uint Orange = 0xFFF76B15;
    public const uint Yellow = 0xFFFFC53D;
    public const uint Green = 0xFF30A46C;
    public const uint Blue = 0xFF0091FF;
    public const uint Purple = 0xFF8E4EC6;
    public const uint White = 0xFFFFFFFF;

    /// <summary>In the order they are shown, carmine first.</summary>
    public static IReadOnlyList<uint> All { get; } =
        [Carmine, Red, Orange, Yellow, Green, Blue, Purple, White];
}
