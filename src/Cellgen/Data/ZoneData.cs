namespace Cellgen;

/// <summary>A spawn zone as the bake recorded it: everything needed to budget the zone.</summary>
public sealed class ZoneData
{
    /// <summary>Gets the zone node's path, relative to the cell root.</summary>
    public string NodePath { get; init; } = string.Empty;

    /// <summary>Gets the zone's centre, in cell-local space.</summary>
    public Vec3 Center { get; init; }

    /// <summary>Gets how the zone releases its encounter.</summary>
    public EncounterMode Mode { get; init; } = EncounterMode.Triggered;

    /// <summary>Gets explicit encounter ids. When non-empty, the zone picks only from these.</summary>
    public IReadOnlyList<string> EncounterIds { get; init; } = [];

    /// <summary>Gets encounter tags. With no explicit ids, the zone picks any encounter carrying one of these, or any at all if empty.</summary>
    public IReadOnlyList<string> EncounterTags { get; init; } = [];

    /// <summary>Gets the zone's share of the level budget, relative to other zones.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Gets the cheapest encounter the zone accepts.</summary>
    public float MinCost { get; init; }

    /// <summary>Gets the costliest encounter the zone accepts; 0 for no limit.</summary>
    public float MaxCost { get; init; }

    /// <summary>Gets a value indicating whether the zone is filled only from budget left over after the required ones.</summary>
    public bool Optional { get; init; }

    /// <summary>Gets the baked spawn points, on walkable floor, in cell-local space.</summary>
    public IReadOnlyList<Vec3> SpawnPoints { get; init; } = [];
}
