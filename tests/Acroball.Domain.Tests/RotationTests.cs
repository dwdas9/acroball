using Acroball.Domain;
using Xunit;

namespace Acroball.Domain.Tests;

public class RotationTests
{
    [Theory]
    [InlineData(Rotation.None, Rotation.Clockwise90, Rotation.Clockwise90)]
    [InlineData(Rotation.Clockwise90, Rotation.Clockwise90, Rotation.Rotate180)]
    [InlineData(Rotation.Rotate180, Rotation.Rotate180, Rotation.None)]
    [InlineData(Rotation.CounterClockwise90, Rotation.Clockwise90, Rotation.None)]
    [InlineData(Rotation.CounterClockwise90, Rotation.Rotate180, Rotation.Clockwise90)]
    public void Add_wraps_around_full_turn(Rotation first, Rotation second, Rotation expected)
        => Assert.Equal(expected, first.Add(second));

    [Fact]
    public void ToDegrees_matches_enum_value()
        => Assert.Equal(270, Rotation.CounterClockwise90.ToDegrees());
}

