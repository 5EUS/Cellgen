namespace Cellgen.Generation;

/// <summary>An encounter the populator is considering for a spawn zone that already accepts it by its own rules.</summary>
/// <param name="Def">The recipe.</param>
/// <param name="Context">The seed, depth, unlock flags and catalogs.</param>
/// <param name="Cell">The cell the zone is in.</param>
/// <param name="Zone">The zone.</param>
/// <param name="Encounter">The encounter.</param>
/// <param name="BudgetLeft">What is left of the level's encounter budget.</param>
/// <param name="FairShare">The zone's share of it.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct EncounterQuery(LevelData Def, GenContext Context, PlannedCell Cell, ZoneData Zone, EncounterEntry Encounter, float BudgetLeft, float FairShare);
