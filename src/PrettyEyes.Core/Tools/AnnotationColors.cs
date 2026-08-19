namespace PrettyEyes.Core.Tools;

/// <summary>
/// Annotation colours live here rather than in the XAML token set, because
/// Core knows nothing about XAML. Carmine, picked from samples on real
/// screenshots: shapes drawn over arbitrary content have to be findable, and
/// the pastel red it replaced looked washed out on both dark and light pages.
/// </summary>
internal static class AnnotationColors
{
    internal const uint Shape = 0xFFB01030;
    internal const float StrokeWidth = 3f;
}
