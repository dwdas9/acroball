using Acroball.Domain;
using Acroball.UI.Services;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class AnnotationCoordinateMapperTests
{
    // Reference page: 200x100 points. Reference points below are each
    // corner of the displayed bitmap, independently derived (twice, by
    // forward-rotating each of the page's four corners and checking the
    // inverse lands back on the expected corner) for every Rotation value —
    // see ADR-0013.

    [Theory]
    [InlineData(0, 0, 0, 100)]
    [InlineData(400, 0, 200, 100)]
    [InlineData(0, 200, 0, 0)]
    [InlineData(400, 200, 200, 0)]
    public void ToPdfPoint_no_rotation(double screenX, double screenY, double expectedX, double expectedY)
    {
        var (x, y) = AnnotationCoordinateMapper.ToPdfPoint(screenX, screenY, displayWidthPx: 400, pageWidthPoints: 200, pageHeightPoints: 100, Rotation.None);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(200, 0, 0, 100)]
    [InlineData(0, 400, 200, 0)]
    [InlineData(200, 400, 200, 100)]
    public void ToPdfPoint_clockwise_90(double screenX, double screenY, double expectedX, double expectedY)
    {
        // Displayed bitmap is the rotated page: width/height swap (200x100 page -> 200x400 display at scale 2).
        var (x, y) = AnnotationCoordinateMapper.ToPdfPoint(screenX, screenY, displayWidthPx: 200, pageWidthPoints: 200, pageHeightPoints: 100, Rotation.Clockwise90);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }

    [Theory]
    [InlineData(0, 0, 200, 0)]
    [InlineData(400, 0, 0, 0)]
    [InlineData(0, 200, 200, 100)]
    [InlineData(400, 200, 0, 100)]
    public void ToPdfPoint_rotate_180(double screenX, double screenY, double expectedX, double expectedY)
    {
        var (x, y) = AnnotationCoordinateMapper.ToPdfPoint(screenX, screenY, displayWidthPx: 400, pageWidthPoints: 200, pageHeightPoints: 100, Rotation.Rotate180);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }

    [Theory]
    [InlineData(0, 0, 200, 100)]
    [InlineData(200, 0, 200, 0)]
    [InlineData(0, 400, 0, 100)]
    [InlineData(200, 400, 0, 0)]
    public void ToPdfPoint_counter_clockwise_90(double screenX, double screenY, double expectedX, double expectedY)
    {
        var (x, y) = AnnotationCoordinateMapper.ToPdfPoint(screenX, screenY, displayWidthPx: 200, pageWidthPoints: 200, pageHeightPoints: 100, Rotation.CounterClockwise90);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }
}
