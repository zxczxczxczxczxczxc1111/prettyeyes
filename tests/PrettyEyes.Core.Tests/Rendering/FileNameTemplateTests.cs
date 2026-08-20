using PrettyEyes.Core.Rendering;
using Xunit;

namespace PrettyEyes.Core.Tests.Rendering;

public class FileNameTemplateTests
{
    private static readonly DateTimeOffset Moment =
        new(2026, 8, 20, 14, 5, 9, TimeSpan.FromHours(3));

    [Fact]
    public void The_default_template_spells_out_the_date_and_time()
    {
        var name = FileNameTemplate.Format(FileNameTemplate.Default, Moment);

        Assert.Equal("prettyeyes-2026-08-20-140509.png", name);
    }

    [Fact]
    public void An_empty_template_falls_back_to_the_default()
    {
        Assert.Equal(
            FileNameTemplate.Format(FileNameTemplate.Default, Moment),
            FileNameTemplate.Format("   ", Moment));
    }

    [Fact]
    public void A_template_without_an_extension_gets_one()
    {
        Assert.Equal("снимок-2026.png", FileNameTemplate.Format("снимок-{ГГГГ}", Moment));
    }

    [Fact]
    public void An_unknown_field_is_left_as_typed()
    {
        // Deleting it silently would leave somebody wondering where their text
        // went; seeing it in the file name says plainly that it is not a field.
        Assert.Equal("shot-{завтра}.png", FileNameTemplate.Format("shot-{завтра}.png", Moment));
    }

    [Fact]
    public void Characters_the_file_system_refuses_are_dropped()
    {
        var name = FileNameTemplate.Format("a<b>c:d\"e/f\\g|h?i*j.png", Moment);

        Assert.Equal("abcdefghij.png", name);
    }

    [Fact]
    public void A_template_that_is_nothing_but_forbidden_characters_falls_back()
    {
        Assert.Equal(
            FileNameTemplate.Format(FileNameTemplate.Default, Moment),
            FileNameTemplate.Format("///???", Moment));
    }

    [Fact]
    public void The_extension_is_not_doubled()
    {
        Assert.Equal("shot.png", FileNameTemplate.Format("shot.PNG", Moment).ToLowerInvariant());
    }
}
