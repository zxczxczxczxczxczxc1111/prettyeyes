namespace PrettyEyes.Core.Tools;

public enum ToolKind
{
    Blur,
    Arrow,
    Line,
    Rectangle,
    Pencil,
    Marker,
    Emoji,

    // Appended, never inserted: the values are the keys of two dictionaries in
    // the settings file, and renumbering them turns somebody's marker into a
    // rectangle on the next launch.
    Text,
}
