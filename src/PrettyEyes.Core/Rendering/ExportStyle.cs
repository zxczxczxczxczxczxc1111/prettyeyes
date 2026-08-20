namespace PrettyEyes.Core.Rendering;

/// <summary>What the screenshot sits on when it leaves the application.</summary>
public enum ExportBackground
{
    Black,
    Gradient,
    White,
    Transparent,
}

/// <summary>
/// Padding, backdrop and rounding applied on the way out.
///
/// None of it touches the overlay: what is being framed is the finished
/// screenshot, and seeing the padding while choosing a region would only make
/// the region harder to choose.
/// </summary>
public sealed record ExportStyle(
    bool Enabled,
    int Padding,
    ExportBackground Background,
    int CornerRadius,
    bool Shadow)
{
    /// <summary>Exactly what every version so far produced.</summary>
    public static ExportStyle None => new(false, 0, ExportBackground.Black, 0, false);

    /// <summary>
    /// A shadow needs somewhere to fall. With no padding it lands outside the
    /// canvas and the only visible effect is a clipped edge.
    /// </summary>
    public bool ShadowAllowed => Padding >= 24;

    /// <summary>
    /// Padding is capped at a quarter of the shorter side and rounding at an
    /// eighth: a 50 pixel selection with 72 pixels of padding is a frame around
    /// nothing.
    /// </summary>
    public ExportStyle FitTo(int width, int height)
    {
        if (!Enabled)
        {
            return None;
        }

        var shorter = Math.Min(width, height);

        return this with
        {
            Padding = Math.Min(Padding, shorter / 4),
            CornerRadius = Math.Min(CornerRadius, shorter / 8),
            Shadow = Shadow && ShadowAllowed,
        };
    }
}
