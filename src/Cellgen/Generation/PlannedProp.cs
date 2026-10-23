namespace Cellgen.Generation;

/// <summary>A prop the populator placed on a prop spawn.</summary>
/// <param name="Index">Its position in the level's list: a stable id for networking.</param>
/// <param name="CellIndex">The cell the spawn is in.</param>
/// <param name="Spawn">The baked spawn.</param>
/// <param name="PropId">The prop's id.</param>
public sealed record PlannedProp(int Index, int CellIndex, PropSpawnData Spawn, string PropId);
