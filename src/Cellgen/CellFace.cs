namespace Cellgen;

/// <summary>
/// A face of a slot. North is forward (<c>-Z</c>); the four
/// horizontal faces are in clockwise order seen from above, so a quarter
/// turn is an index step.
///
/// The values are serialized into every cell scene's sockets. Append, never
/// renumber.
/// </summary>
public enum CellFace
{
    /// <summary>The <c>-Z</c> face.</summary>
    North = 0,

    /// <summary>The <c>+X</c> face.</summary>
    East = 1,

    /// <summary>The <c>+Z</c> face.</summary>
    South = 2,

    /// <summary>The <c>-X</c> face.</summary>
    West = 3,

    /// <summary>The <c>+Y</c> face: a shaft or ramp to the layer above.</summary>
    Up = 4,

    /// <summary>The <c>-Y</c> face: a shaft or ramp to the layer below.</summary>
    Down = 5,
}
