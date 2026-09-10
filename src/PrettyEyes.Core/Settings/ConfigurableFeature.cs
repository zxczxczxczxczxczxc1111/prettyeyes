using PrettyEyes.Core.Tools;

namespace PrettyEyes.Core.Settings;

/// <summary>Which row of the settings an icon belongs to.</summary>
public enum FeatureGroup
{
    DrawingTool,
    Feature,
}

/// <summary>
/// Everything with an icon in the settings window. Drawing tools keep their own
/// enum because they end up in the settings file; this one never does, so
/// values may be reordered without eating anybody's configuration.
/// </summary>
public enum FeatureId
{
    Blur,
    Arrow,
    Line,
    Rectangle,
    Pencil,
    Marker,
    Emoji,
    Text,
    Magnifier,
    Cursor,
    QuickSave,
    Export,
    Pin,
    Laser,
}

/// <summary>
/// What the corner, and the right button, opens for an icon. A card per entry
/// rather than a flag: the overlay already knew that emoji has a grid of glyphs
/// where the others have colours, and the settings window did not, so it opened
/// the colour card for emoji. A fork written down once cannot disagree with
/// itself.
/// </summary>
public enum FeatureCard
{
    None,
    Style,
    Emoji,
    Magnifier,
    Cursor,
    QuickSave,
    Export,
    Pin,
    Laser,
}

/// <summary>
/// One icon in the settings: which group it sits in, which card right-clicking
/// it opens, and the tool it stands for when it stands for one.
/// </summary>
public sealed record ConfigurableFeature(
    FeatureId Id,
    FeatureGroup Group,
    FeatureCard Card,
    ToolKind? Tool)
{
    /// <summary>Whether right-clicking it has anything to show at all.</summary>
    public bool HasSettings => Card != FeatureCard.None;

    private static readonly Dictionary<ToolKind, FeatureId> Ids = new()
    {
        [ToolKind.Blur] = FeatureId.Blur,
        [ToolKind.Arrow] = FeatureId.Arrow,
        [ToolKind.Line] = FeatureId.Line,
        [ToolKind.Rectangle] = FeatureId.Rectangle,
        [ToolKind.Pencil] = FeatureId.Pencil,
        [ToolKind.Marker] = FeatureId.Marker,
        [ToolKind.Emoji] = FeatureId.Emoji,
        [ToolKind.Text] = FeatureId.Text,
    };

    /// <summary>
    /// Drawing tools come from ToolVisibility.All so the two lists cannot drift
    /// apart: a tool added there shows up here without anybody remembering to.
    /// </summary>
    public static IReadOnlyList<ConfigurableFeature> All { get; } =
    [
        .. ToolVisibility.All.Select(tool => new ConfigurableFeature(
            Ids[tool],
            FeatureGroup.DrawingTool,
            CardFor(tool),
            tool)),

        new(FeatureId.Magnifier, FeatureGroup.Feature, FeatureCard.Magnifier, Tool: null),
        new(FeatureId.Cursor, FeatureGroup.Feature, FeatureCard.Cursor, Tool: null),
        new(FeatureId.QuickSave, FeatureGroup.Feature, FeatureCard.QuickSave, Tool: null),
        new(FeatureId.Export, FeatureGroup.Feature, FeatureCard.Export, Tool: null),
        new(FeatureId.Pin, FeatureGroup.Feature, FeatureCard.Pin, Tool: null),

        // Not a ToolKind and never will be. It draws on the screen and leaves
        // nothing behind: as a tool it would go into the document, the undo
        // stack and the exported picture, none of which it belongs in.
        new(FeatureId.Laser, FeatureGroup.Feature, FeatureCard.Laser, Tool: null),
    ];

    /// <summary>
    /// Which card a drawing tool opens. Two of them are not the colour card:
    /// blur has no colour and no width at all, and emoji picks a glyph, which
    /// is what the overlay has always opened for it.
    /// </summary>
    private static FeatureCard CardFor(ToolKind tool) => tool switch
    {
        ToolKind.Blur => FeatureCard.None,
        ToolKind.Emoji => FeatureCard.Emoji,
        _ => FeatureCard.Style,
    };

    /// <summary>
    /// What may be picked as the tool a capture starts with. Emoji is out: with
    /// no glyph chosen it opens the picker instead of drawing, which is a
    /// strange way to greet a new selection.
    /// </summary>
    public static IReadOnlyList<ToolKind> DefaultToolChoices { get; } =
    [
        .. All.Where(f => f.Group == FeatureGroup.DrawingTool && f.Tool != ToolKind.Emoji)
              .Select(f => f.Tool!.Value),
    ];
}
