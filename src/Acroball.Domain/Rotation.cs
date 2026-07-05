namespace Acroball.Domain;

/// <summary>
/// A page rotation, expressed as the clockwise angle in degrees.
/// </summary>
/// <remarks>
/// The numeric values intentionally match the degree values stored in a PDF
/// page's <c>/Rotate</c> entry so backends can cast directly.
/// </remarks>
public enum Rotation
{
    /// <summary>No rotation.</summary>
    None = 0,

    /// <summary>Rotate 90Â° clockwise.</summary>
    Clockwise90 = 90,

    /// <summary>Rotate 180Â°.</summary>
    Rotate180 = 180,

    /// <summary>Rotate 90Â° counter-clockwise (270Â° clockwise).</summary>
    CounterClockwise90 = 270,
}

/// <summary>Helpers for composing <see cref="Rotation"/> values.</summary>
public static class RotationExtensions
{
    /// <summary>Combines two rotations, wrapping around a full turn.</summary>
    public static Rotation Add(this Rotation first, Rotation second)
        => (Rotation)(((int)first + (int)second) % 360);

    /// <summary>Returns the clockwise angle in degrees.</summary>
    public static int ToDegrees(this Rotation rotation) => (int)rotation;
}

