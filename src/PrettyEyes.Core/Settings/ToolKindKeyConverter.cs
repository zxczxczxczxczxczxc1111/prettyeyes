using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrettyEyes.Core.Tools;

namespace PrettyEyes.Core.Settings;

/// <summary>
/// Numbers instead of names for tool keys.
///
/// System.Text.Json writes enum dictionary keys as names, and a name a previous
/// version has never heard of does not read as "unknown", it throws. Since Load
/// answers a throw with "here are your default settings", one new tool would
/// erase every hotkey the user ever set the moment they rolled back.
/// </summary>
public sealed class ToolKindKeyConverter : JsonConverter<ToolKind>
{
    // Values keep the stock numeric behaviour. Nothing clever here on purpose.
    public override ToolKind Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => (ToolKind)reader.GetInt32();

    public override void Write(Utf8JsonWriter writer, ToolKind value, JsonSerializerOptions options)
        => writer.WriteNumberValue((int)value);

    /// <summary>
    /// Numbers going out, numbers OR 1.2-era names coming in. Drop the name
    /// branch and every existing install loses its settings on update: that is
    /// today's file, not some hypothetical downgrade.
    /// </summary>
    public override ToolKind ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? throw new JsonException("Empty tool key.");

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return (ToolKind)number;
        }

        return Enum.TryParse<ToolKind>(raw, ignoreCase: false, out var named)
            ? named
            : throw new JsonException($"Unknown tool key '{raw}'.");
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ToolKind value, JsonSerializerOptions options)
        => writer.WritePropertyName(((int)value).ToString(CultureInfo.InvariantCulture));
}
