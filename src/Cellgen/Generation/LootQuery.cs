namespace Cellgen.Generation;

/// <summary>A loot entry the populator is considering for an item spawn that already accepts it by its own rules.</summary>
/// <param name="Def">The recipe.</param>
/// <param name="Context">The seed, depth, unlock flags and catalogs.</param>
/// <param name="Cell">The cell the spawn is in.</param>
/// <param name="Spawn">The item spawn.</param>
/// <param name="Entry">The entry.</param>
/// <param name="BudgetLeft">What is left of the loot budget; zero for shop and ammo spawns, which cost none.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct LootQuery(LevelData Def, GenContext Context, PlannedCell Cell, ItemSpawnData Spawn, LootEntryData Entry, float BudgetLeft);
