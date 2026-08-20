namespace PrettyEyes.Core.Model;

/// <summary>
/// An annotation that can be picked up and put somewhere else.
///
/// Not every annotation wants this. An arrow is defined by where it starts and
/// where it points, and dragging it as a block is not what anyone means by
/// moving an arrow; a stamped glyph has no such argument with itself.
/// </summary>
public interface IMovable : IAnnotation
{
    /// <summary>The same object, that much further along. Never mutates.</summary>
    IMovable MovedBy(int dx, int dy);

    /// <summary>
    /// A step bigger or smaller, around its own centre. Null when it has
    /// nowhere left to go in that direction.
    /// </summary>
    IMovable? ResizedBy(int steps);
}
