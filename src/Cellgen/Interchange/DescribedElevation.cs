namespace Cellgen.Interchange;

/// <summary>
/// Hilly ground for a described layout. Give <see cref="Heights"/> to set the ground yourself, or leave it out and the
/// generator rolls the same rolling field the built-in <c>hills</c> layout does, level under the start, the exit, the
/// structures and any slot reserved for a special cell, from <see cref="Relief"/>, <see cref="Scale"/> and
/// <see cref="Plateaus"/>. The solver then places only cells whose corner heights fit (see <see cref="Generation.ElevationField"/>).
///
/// <code>
/// { "step": 8, "relief": 7, "scale": 6, "plateaus": 4 }                        // rolled
/// { "step": 8, "width": 4, "heights": [0,0,1,1,  0,1,2,1,  0,1,1,0] }          // explicit: 4 corners across, 3 deep
/// </code>
/// </summary>
public sealed class DescribedElevation
{
    /// <summary>Gets or sets how many metres one height step is. Must be above zero.</summary>
    public float Step { get; set; }

    /// <summary>Gets or sets the corners along X of <see cref="Heights"/>. Needed with explicit heights.</summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the ground height at every slot corner in steps, row-major <c>z * width + x</c>; empty to roll one. It must cover every slot's corners, so at least one more than the slots along each axis.</summary>
    public IReadOnlyList<int> Heights { get; set; } = [];

    /// <summary>Gets or sets the tallest rolled ground, in steps.</summary>
    public int Relief { get; set; } = 6;

    /// <summary>Gets or sets how many corners across one coarse rolled hill is; smaller is choppier.</summary>
    public int Scale { get; set; } = 6;

    /// <summary>Gets or sets how many level 2x2-slot plateaus a rolled field has; negative for one per 45 slots.</summary>
    public int Plateaus { get; set; } = -1;
}
