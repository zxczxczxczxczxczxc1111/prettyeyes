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
}

/// <summary>
/// One icon in the settings: which group it sits in, whether right-clicking it
/// has anything to show, and the tool it stands for when it stands for one.
/// </summary>
public sealed record ConfigurableFeature(
    FeatureId Id,
    FeatureGroup Group,
    bool HasSettings,
    ToolKind? Tool)
{
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

            // Blur is the odd one out: no colour, no width, nothing to open.
            HasSettings: tool != ToolKind.Blur,
            tool)),

        new(FeatureId.Magnifier, FeatureGroup.Feature, HasSettings: true, Tool: null),
        new(FeatureId.Cursor, FeatureGroup.Feature, HasSettings: true, Tool: null),
        new(FeatureId.QuickSave, FeatureGroup.Feature, HasSettings: true, Tool: null),
        new(FeatureId.Export, FeatureGroup.Feature, HasSettings: true, Tool: null),
        new(FeatureId.Pin, FeatureGroup.Feature, HasSettings: true, Tool: null),
    ];

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
