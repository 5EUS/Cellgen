namespace Cellgen.Generation;

/// <summary>One cell of a finished level.</summary>
/// <param name="Index">Its index in <see cref="LevelPlan.Cells"/>.</param>
/// <param name="Info">The baked cell.</param>
/// <param name="Anchor">The rotated footprint's minimum slot.</param>
/// <param name="Rotation">Quarter turns.</param>
/// <param name="StructureId">The structure it belongs to, or empty.</param>
/// <param name="Elevation">How many elevation steps (<see cref="LevelPlan.ElevationStep"/> metres each) the cell is lifted above its slot's floor plane. Zero on a level with level ground.</param>
public sealed record PlannedCell(int Index, CellData Info, Int3 Anchor, int Rotation, string StructureId, int Elevation = 0)
{
    /// <summary>Gets the footprint after rotation.</summary>
    public Int3 Footprint => SlotMath.RotateFootprint(this.Info.Footprint, this.Rotation);

    /// <summary>
    /// Gets the ground height at each corner of the rotated footprint as placed, in elevation steps above the level's base:
    /// the cell's own corner heights plus its <see cref="Elevation"/>. Row-major over <c>Footprint.X + 1</c> by <c>Footprint.Z + 1</c>.
    /// </summary>
    /// <returns>The heights.</returns>
    public IReadOnlyList<int> AbsoluteCorners()
    {
        int expected = (this.Info.Footprint.X + 1) * (this.Info.Footprint.Z + 1);
        int[] corners = SlotMath.RotateCorners(this.Info.CornerHeights.Count == expected ? this.Info.CornerHeights : [], this.Info.Footprint, this.Rotation);
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] += this.Elevation;
        }

        return corners;
    }

    /// <summary>Gets the faces of this cell that open onto something, as placed: slots are within the rotated footprint.</summary>
    /// <returns>The open sockets, in a stable order.</returns>
    public IReadOnlyList<OpenSocket> OpenSockets()
    {
        var variant = new CellVariant(new CellTemplate(this.Info, 1f, 1 << this.Rotation), this.Rotation);
        var open = new List<OpenSocket>();
        foreach ((Int3 slot, CellFace face) in variant.OuterFaces)
        {
            if (variant.SocketAt(slot, face) is { } socket)
            {
                open.Add(new OpenSocket(slot, face, socket));
            }
        }

        return open;
    }
}
