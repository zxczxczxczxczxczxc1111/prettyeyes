namespace PrettyEyes.Core.Tools;

/// <summary>How thick a drawn shape is. Three steps, not a slider.</summary>
public enum StrokeSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// What a tool draws with. Blur is deliberately absent from this: its radius
/// comes from the size of the region and is always strong enough, because weak
/// blur is reversible and the tool exists to hide data.
/// </summary>
public sealed record ToolStyle(uint Color, StrokeSize Size)
{
    /// <summary>Carmine at medium, which is what every version so far drew.</summary>
    public static ToolStyle Default => new(Palette.Carmine, StrokeSize.Medium);

    /// <summary>
    /// What a tool draws with before anyone has an opinion. Only the
    /// highlighter differs: carmine multiplied into a page is a wound, and the
    /// colour everyone reaches for on paper is yellow.
    /// </summary>
    public static ToolStyle DefaultFor(ToolKind kind) => kind switch
    {
        ToolKind.Marker => new(Palette.Yellow, StrokeSize.Medium),
        _ => Default,
    };

    /// <summary>Stroke width in physical pixels.</summary>
    public float StrokeWidth => Size switch
    {
        StrokeSize.Small => 2f,
        StrokeSize.Large => 5f,
        _ => 3f,
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
