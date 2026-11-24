namespace Cellgen.Generation;

/// <summary>
/// The ground height at every slot corner of a level, in whole steps of <see cref="StepMetres"/>. Neighbouring slots share
/// corners, so a field cannot disagree with itself along a shared edge: a cell is placed only where every corner of its
/// footprint sits one constant lift above the cell's own <see cref="CellData.CornerHeights"/>, and that lift is the cell's
/// elevation. Corner <c>(x, z)</c> is the north-west corner of slot <c>(x, *, z)</c>. Columns, not layers: the field ignores
/// a slot's Y.
/// </summary>
public sealed class ElevationField
{
    private readonly int[] heights;

    /// <summary>Initializes a new instance of the <see cref="ElevationField"/> class.</summary>
    /// <param name="corners">Corners along X and Z: one more than the slots they bound.</param>
    /// <param name="stepMetres">How tall one height step is, in metres.</param>
    /// <param name="heights">Row-major heights, <c>z * corners.X + x</c>.</param>
    public ElevationField(Int2 corners, float stepMetres, IReadOnlyList<int> heights)
    {
        ArgumentNullException.ThrowIfNull(heights);
        if (corners.X < 2 || corners.Y < 2 || heights.Count != corners.X * corners.Y)
        {
            throw new ArgumentException($"a {corners.X}x{corners.Y} corner grid needs {corners.X * corners.Y} heights, got {heights.Count}", nameof(heights));
        }

        this.Corners = corners;
        this.StepMetres = stepMetres;
        this.heights = [.. heights];
    }

    /// <summary>Gets the corners along X and Z.</summary>
    public Int2 Corners { get; }

    /// <summary>Gets how tall one height step is, in metres.</summary>
    public float StepMetres { get; }

    /// <summary>Gets the height at a corner.</summary>
    /// <param name="x">The corner's X.</param>
    /// <param name="z">The corner's Z.</param>
    /// <returns>The height in steps.</returns>
    public int Get(int x, int z) => this.heights[(z * this.Corners.X) + x];

    /// <summary>Gets whether a corner lies inside the field.</summary>
    /// <param name="x">The corner's X.</param>
    /// <param name="z">The corner's Z.</param>
    /// <returns>True when the field covers it.</returns>
    public bool Contains(int x, int z) => x >= 0 && z >= 0 && x < this.Corners.X && z < this.Corners.Y;
}
