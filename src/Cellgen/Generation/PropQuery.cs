namespace Cellgen.Generation;

/// <summary>A prop the populator is considering for a prop spawn.</summary>
/// <param name="Def">The recipe.</param>
/// <param name="Context">The seed, depth, unlock flags and catalogs.</param>
/// <param name="Cell">The cell the spawn is in.</param>
/// <param name="Spawn">The prop spawn.</param>
/// <param name="PropId">The prop's id.</param>
/// <param name="PlacedSoFar">How many props the level already has.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PropQuery(LevelData Def, GenContext Context, PlannedCell Cell, PropSpawnData Spawn, string PropId, int PlacedSoFar);
