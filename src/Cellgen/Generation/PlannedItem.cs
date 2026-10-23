namespace Cellgen.Generation;

/// <summary>An item the populator rolled for an item spawn.</summary>
/// <param name="CellIndex">The cell the spawn is in.</param>
/// <param name="Spawn">The baked spawn.</param>
/// <param name="ItemId">The item's id.</param>
/// <param name="Price">Its shop price; 0 for a free pickup.</param>
public sealed record PlannedItem(int CellIndex, ItemSpawnData Spawn, string ItemId, float Price);
