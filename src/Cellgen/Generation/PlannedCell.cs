namespace Cellgen.Generation;

/// <summary>One cell of a finished level.</summary>
/// <param name="Index">Its index in <see cref="LevelPlan.Cells"/>.</param>
/// <param name="Info">The baked cell.</param>
/// <param name="Anchor">The rotated footprint's minimum slot.</param>
/// <param name="Rotation">Quarter turns.</param>
/// <param name="StructureId">The structure it belongs to, or empty.</param>
public sealed record PlannedCell(int Index, CellData Info, Int3 Anchor, int Rotation, string StructureId)
{
    /// <summary>Gets the footprint after rotation.</summary>
    public Int3 Footprint => SlotMath.RotateFootprint(this.Info.Footprint, this.Rotation);

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
