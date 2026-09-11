using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Tests.Geometry;

public class WindowShapesTests
{
    [Fact]
    public void EmptySnapshotFindsNothing()
    {
        Assert.Null(WindowShapes.None.At(100, 100));
    }

    [Fact]
    public void APointOutsideEveryWindowFindsNothing()
    {
        var shapes = new WindowShapes([new CaptureRect(0, 0, 100, 100)]);

        Assert.Null(shapes.At(200, 200));
    }

    [Fact]
    public void TheWindowInFrontWins()
    {
        // Front to back, the order EnumWindows hands them over in. Both cover
        // the point; the answer is the one that is actually visible there.
        var shapes = new WindowShapes(
        [
            new CaptureRect(40, 40, 100, 100),
            new CaptureRect(0, 0, 400, 400),
        ]);

        Assert.Equal(new CaptureRect(40, 40, 100, 100), shapes.At(50, 50));
    }

    [Fact]
    public void APointOutsideTheFrontWindowFallsThroughToTheOneBehind()
    {
        var shapes = new WindowShapes(
        [
            new CaptureRect(40, 40, 100, 100),
            new CaptureRect(0, 0, 400, 400),
        ]);

        Assert.Equal(new CaptureRect(0, 0, 400, 400), shapes.At(10, 10));
    }

    [Fact]
    public void EdgesBelongToTheWindow()
    {
        var shapes = new WindowShapes([new CaptureRect(10, 10, 100, 100)]);

        Assert.NotNull(shapes.At(10, 10));
        Assert.NotNull(shapes.At(109, 109));
        Assert.Null(shapes.At(110, 110));
    }

    [Fact]
    public void EmptyRectanglesAreNotWindows()
    {
        // A window with no size answers every point inside its own corner
        // otherwise, and there is nothing to select.
        var shapes = new WindowShapes([new CaptureRect(10, 10, 0, 0)]);

        Assert.Null(shapes.At(10, 10));
    }

    [Fact]
    public void NegativeCoordinatesWork()
    {
        // The second monitor can sit to the left of the primary one, and then
        // the whole virtual desktop starts at a negative x.
        var shapes = new WindowShapes([new CaptureRect(-1920, 0, 800, 600)]);

        Assert.Equal(new CaptureRect(-1920, 0, 800, 600), shapes.At(-1900, 100));
    }
}
