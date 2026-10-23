namespace Cellgen.Generation;

/// <summary>A spawn zone the populator gave an encounter.</summary>
/// <param name="Handle">The zone's handle in the simulation; unique within the level.</param>
/// <param name="CellIndex">The cell the zone is in.</param>
/// <param name="Zone">The baked zone.</param>
/// <param name="EncounterId">The encounter.</param>
/// <param name="Cost">What it spent of the budget.</param>
/// <param name="Seed">The zone's own seed, so its spawns are reproducible.</param>
public sealed record PlannedZone(int Handle, int CellIndex, ZoneData Zone, string EncounterId, float Cost, ulong Seed);
