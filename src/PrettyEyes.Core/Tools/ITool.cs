using PrettyEyes.Core.Model;

namespace PrettyEyes.Core.Tools;

/// <summary>
/// A drawing gesture: pressed, dragging, released.
/// Adding a tool means one class here and one button in the toolbar.
/// </summary>
public interface ITool
{
    void Begin(int x, int y);

    /// <summary>
    /// The shape as it would look if released now. Drawn on top of the canvas
    /// while dragging, never stored in the document.
    /// </summary>
    IAnnotation? Preview(int x, int y);

    /// <summary>
    /// Finishes the gesture. Null when it produced nothing - a bare click, or
    /// a drag with zero area.
    /// </summary>
    IAnnotation? End(int x, int y);
}
