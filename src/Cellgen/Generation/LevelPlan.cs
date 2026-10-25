using System.Globalization;
using System.Text;

namespace Cellgen.Generation;

/// <summary>
/// <para>
/// A finished level as plain data: which cells go where, which zone gets
/// which encounter, which spawn gets which item. Nothing here is a node.
/// </para>
/// <para>
/// This is the seam between generating a level and building one. The
/// editor preview draws it, <c>tools/procgen_check.gd</c> fingerprints it,
/// the console dumps it, and <c>LevelBuilder</c> turns it into a scene; the
/// same recipe, seed and depth always give the same plan.
/// </para>
/// </summary>
public sealed class LevelPlan
{
    /// <summary>Gets the recipe.</summary>
    public required LevelData Def { get; init; }

    /// <summary>Gets the seed the level was asked for.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Gets the seed of the attempt that succeeded.</summary>
    public required ulong AttemptSeed { get; init; }

    /// <summary>Gets the run depth.</summary>
    public required int Depth { get; init; }

    /// <summary>Gets the cells, sorted by anchor.</summary>
    public required IReadOnlyList<PlannedCell> Cells { get; init; }

    /// <summary>Gets the start-to-exit path, in slots.</summary>
    public required IReadOnlyList<Int3> CriticalPath { get; init; }

    /// <summary>Gets the index of the start cell.</summary>
    public required int StartCell { get; init; }

    /// <summary>Gets the index of the exit cell.</summary>
    public required int ExitCell { get; init; }

    /// <summary>Gets every zone that was given an encounter.</summary>
    public required IReadOnlyList<PlannedZone> Zones { get; init; }

    /// <summary>Gets every item rolled.</summary>
    public required IReadOnlyList<PlannedItem> Items { get; init; }

    /// <summary>Gets every prop placed.</summary>
    public required IReadOnlyList<PlannedProp> Props { get; init; }

    /// <summary>Gets how many zones were left empty.</summary>
    public required int EmptyZones { get; init; }

    /// <summary>Gets the encounter budget, after depth scaling.</summary>
    public required float EncounterBudget { get; init; }

    /// <summary>Gets how much of it the zones spent.</summary>
    public required float EncounterSpent { get; init; }

    /// <summary>Gets the loot budget, after depth scaling.</summary>
    public required float LootBudget { get; init; }

    /// <summary>Gets how much of it the pickups spent.</summary>
    public required float LootSpent { get; init; }

    /// <summary>
    /// A hash of everything the plan decided. Two plans with the same
    /// fingerprint build the same level - the determinism check compares
    /// these across runs.
    /// </summary>
    /// <returns>The fingerprint, as 16 hex digits.</returns>
    public string Fingerprint() => StableHash.Of(this.Canonical()).ToString("x16", CultureInfo.InvariantCulture);

    private string Canonical()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{this.Def.Id}|{this.Seed}|{this.Depth}|{this.StartCell}|{this.ExitCell}\n");
        foreach (PlannedCell cell in this.Cells)
        {
            text.Append(CultureInfo.InvariantCulture, $"c {cell.Info.ScenePath} {cell.Anchor} {cell.Rotation} {cell.StructureId}\n");
        }

        foreach (PlannedZone zone in this.Zones)
        {
            text.Append(CultureInfo.InvariantCulture, $"z {zone.Handle} {zone.CellIndex} {zone.Zone.NodePath} {zone.EncounterId} {zone.Seed}\n");
        }

        foreach (PlannedItem item in this.Items)
        {
            text.Append(CultureInfo.InvariantCulture, $"i {item.CellIndex} {item.Spawn.NodePath} {item.ItemId} {item.Price}\n");
        }

        foreach (PlannedProp prop in this.Props)
        {
            text.Append(CultureInfo.InvariantCulture, $"a {prop.CellIndex} {prop.Spawn.NodePath} {prop.PropId}\n");
        }

        return text.ToString();
    }
}
