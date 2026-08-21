using System.Text.Json;
using PrettyEyes.Core.Settings;
using PrettyEyes.Core.Tools;
using Xunit;

namespace PrettyEyes.Core.Tests.Settings;

public class ToolKindKeyConverterTests
{
    private static JsonSerializerOptions Options => new() { Converters = { new ToolKindKeyConverter() } };

    [Fact]
    public void Keys_are_written_as_numbers()
    {
        var json = JsonSerializer.Serialize(
            new Dictionary<ToolKind, bool> { [ToolKind.Arrow] = true }, Options);

        Assert.Contains("\"1\"", json);
        Assert.DoesNotContain("Arrow", json);
    }

    [Fact]
    public void Named_keys_of_an_older_file_still_read()
    {
        // Every install on disk right now has names in it. Refusing them here
        // wipes the user's settings on update, which is a fun way to ship.
        var read = JsonSerializer.Deserialize<Dictionary<ToolKind, bool>>(
            """{ "Marker": false }""", Options);

        Assert.False(read![ToolKind.Marker]);
    }

    [Fact]
    public void Scalar_value_stays_a_number()
    {
        var json = JsonSerializer.Serialize(ToolKind.Emoji, Options);

        Assert.Equal("6", json);
    }
}
