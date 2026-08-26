namespace PrettyEyes.Core.Geometry;

/// <summary>
/// Everything a pointer event has to know before it can be called a repeat.
///
/// A mouse reports far more often than the compositor draws, and the same
/// physical pixel arrives dozens of times in a row. Skipping those is worth a
/// lot: the hover branch samples the picture twice and re-measures a control.
///
/// The pixel alone is not enough, and every field below is here because
/// leaving it out broke something: Modifiers because Ctrl over a glyph changes
/// the cursor without moving it, Dragging because letting go puts a toolbar
/// where the magnifier is, Mode because an armed tool changes what the pointer
/// means, Selection because the arrow keys move the frame under a still cursor.
///
/// Modifiers and Mode are plain ints: they come from Avalonia and from the App
/// layer, and Core has no business referencing either.
/// </summary>
public readonly record struct PointerEcho(
    int X,
    int Y,
    int Modifiers,
    int Mode,
    bool Dragging,
    CaptureRect Selection);
