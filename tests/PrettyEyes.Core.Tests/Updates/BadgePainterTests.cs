using PrettyEyes.Core.Updates;
using Xunit;

namespace PrettyEyes.Core.Tests.Updates;

/// <summary>
/// The mark stamped onto the tray icon.
///
/// Nobody can read a 16x16 image in a test, so these check the properties that
/// actually break: the mark stays in its corner, it does not eat the icon, it
/// is opaque enough to see, and it grows with the icon instead of staying a
/// speck on a tray drawing at 24.
/// </summary>
public class BadgePainterTests
{
    private const byte Ground = 0x20;

    [Fact]
    public void The_mark_keeps_to_the_bottom_right_corner()
    {
        var side = 16;
        var pixels = Solid(side, Ground);

        BadgePainter.Stamp(pixels, side);

        foreach (var (x, y) in Changed(pixels, side))
        {
            Assert.True(x >= side / 2, $"pixel ({x},{y}) is in the left half");
            Assert.True(y >= side / 2, $"pixel ({x},{y}) is in the top half");
        }
    }

    /// <summary>
    /// The icon has to stay recognisable: a mark covering a third of a
    /// 16-point square is not a mark, it is a different icon.
    /// </summary>
    [Fact]
    public void The_mark_is_a_dot_and_not_a_repaint()
    {
        var side = 16;
        var pixels = Solid(side, Ground);

        BadgePainter.Stamp(pixels, side);

        Assert.InRange(Changed(pixels, side).Count, side * side / 12, side * side / 5);
    }

    [Fact]
    public void The_middle_of_the_mark_is_the_accent_and_nothing_shows_through()
    {
        var side = 16;
        var pixels = new byte[side * side * 4];

        BadgePainter.Stamp(pixels, side);

        Assert.Contains(All(side), p => At(pixels, side, p.x, p.y) == BadgePainter.Fill);
    }

    /// <summary>
    /// Without the outline the violet dot dissolves into a light taskbar, and
    /// a taskbar is the one place this icon is ever seen. Antialiasing means no
    /// pixel holds the outline colour exactly, so the check is that the mark
    /// darkens towards its edge at all.
    /// </summary>
    [Fact]
    public void A_darker_ring_separates_the_mark_from_whatever_is_behind_it()
    {
        var side = 16;
        var pixels = new byte[side * side * 4];

        BadgePainter.Stamp(pixels, side);

        Assert.Contains(
            All(side).Select(p => At(pixels, side, p.x, p.y)),
            colour => Alpha(colour) == 0xFF && Luminance(colour) < Luminance(BadgePainter.Fill) - 10);
    }

    /// <summary>
    /// The tray draws at 20 points at 125% and 24 at 150%. A mark sized in
    /// pixels for 16 is a speck there, so the geometry is a fraction of the
    /// side.
    /// </summary>
    [Fact]
    public void A_bigger_icon_gets_a_bigger_mark()
    {
        var small = Solid(16, Ground);
        var large = Solid(32, Ground);

        BadgePainter.Stamp(small, 16);
        BadgePainter.Stamp(large, 32);

        Assert.True(
            Changed(large, 32).Count > Changed(small, 16).Count * 2,
            "the mark did not scale with the icon");
    }

    [Fact]
    public void The_far_corner_of_the_icon_is_never_touched()
    {
        var side = 24;
        var pixels = Solid(side, 0x7A);

        BadgePainter.Stamp(pixels, side);

        for (var y = 0; y < side / 2; y++)
        {
            for (var x = 0; x < side / 2; x++)
            {
                Assert.Equal(0x7A7A7A7Au, At(pixels, side, x, y));
            }
        }
    }

    private static byte[] Solid(int side, byte value)
    {
        var pixels = new byte[side * side * 4];
        Array.Fill(pixels, value);

        return pixels;
    }

    private static uint At(byte[] pixels, int side, int x, int y)
    {
        var offset = ((y * side) + x) * 4;

        return ((uint)pixels[offset + 3] << 24)
            | ((uint)pixels[offset + 2] << 16)
            | ((uint)pixels[offset + 1] << 8)
            | pixels[offset];
    }

    private static byte Alpha(uint colour) => (byte)(colour >> 24);

    private static double Luminance(uint colour) =>
        (0.299 * ((colour >> 16) & 0xFF)) + (0.587 * ((colour >> 8) & 0xFF)) + (0.114 * (colour & 0xFF));

    /// <summary>
    /// The ground is one repeated byte, so a pixel differing from it is a
    /// pixel the mark touched.
    /// </summary>
    private static List<(int x, int y)> Changed(byte[] pixels, int side)
    {
        var ground = At(Solid(side, pixels[0]), side, 0, 0);

        return All(side).Where(p => At(pixels, side, p.x, p.y) != ground).ToList();
    }

    private static List<(int x, int y)> All(int side) =>
        Enumerable.Range(0, side).SelectMany(y => Enumerable.Range(0, side).Select(x => (x, y))).ToList();
}
