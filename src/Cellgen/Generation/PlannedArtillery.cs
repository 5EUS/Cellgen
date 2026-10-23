namespace Cellgen.Generation;

/// <summary>An artillery piece the populator placed on an artillery spawn.</summary>
/// <param name="Index">Its position in the level's list: a stable id for networking.</param>
/// <param name="CellIndex">The cell the spawn is in.</param>
/// <param name="Spawn">The baked spawn.</param>
/// <param name="PieceId">The piece's id.</param>
public sealed record PlannedArtillery(int Index, int CellIndex, ArtillerySpawnData Spawn, string PieceId);
