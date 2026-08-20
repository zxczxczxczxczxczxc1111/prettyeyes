using System.Text;

namespace PrettyEyes.Core.Rendering;

/// <summary>
/// Turns the name template from the settings into a file name.
///
/// The fields are in Russian because the field is typed by hand and read back
/// by the same person: {ГГГГ} says what it is without a legend, {yyyy} does not.
/// </summary>
public static class FileNameTemplate
{
    public const string Default = "prettyeyes-{ГГГГ}-{ММ}-{ДД}-{ЧЧ}{мм}{сс}.png";

    private static readonly (string Field, string Format)[] Fields =
    [
        ("{ГГГГ}", "yyyy"),
        ("{ММ}", "MM"),
        ("{ДД}", "dd"),
        ("{ЧЧ}", "HH"),
        ("{мм}", "mm"),
        ("{сс}", "ss"),
    ];

    public static string Format(string template, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            template = Default;
        }

        var name = template;

        foreach (var (field, format) in Fields)
        {
            name = name.Replace(field, now.ToString(format), StringComparison.Ordinal);
        }

        name = Sanitize(name);

        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            name += ".png";
        }

        // Everything was punctuation the file system refuses: fall back rather
        // than write a file called ".png".
        return name == ".png" ? Format(Default, now) : name;
    }

    /// <summary>
    /// Anything the file system will not take is dropped. An unknown field like
    /// {завтра} stays as it is: it is text somebody typed on purpose, and
    /// silently deleting it would be worse than seeing it in the file name.
    /// </summary>
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new StringBuilder(name.Length);

        foreach (var symbol in name)
        {
            if (Array.IndexOf(invalid, symbol) < 0)
            {
                clean.Append(symbol);
            }
        }

        return clean.ToString().Trim();
    }
}
