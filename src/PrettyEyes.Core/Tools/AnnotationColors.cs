namespace PrettyEyes.Core.Tools;

/// <summary>
/// Annotation colours live here rather than in the XAML token set, because
/// Core knows nothing about XAML. The value mirrors --danger from the design
/// tokens: shapes drawn on an arbitrary screenshot have to be findable, and
/// the monochrome palette disappears against real content.
/// </summary>
internal static class AnnotationColors
{
    internal const uint Shape = 0xFFF87171;
    internal const float StrokeWidth = 3f;
}
