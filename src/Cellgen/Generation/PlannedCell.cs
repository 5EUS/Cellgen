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
}
