namespace PrettyEyes.Core.Geometry;

/// <summary>
/// What part of the selection a pointer is over. Inside means the whole
/// rectangle moves, None means the pointer is nowhere near it and a fresh
/// selection starts instead.
/// </summary>
public enum SelectionGrip
{
    None,
    Inside,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}
