namespace Cellgen;

/// <summary>An item spawn as the bake recorded it.</summary>
public sealed class ItemSpawnData
{
    /// <summary>Gets the spawn node's path, relative to the cell root.</summary>
    public string NodePath { get; init; } = string.Empty;

    /// <summary>Gets the spawn's position, in cell-local space.</summary>
    public Vec3 Position { get; init; }

    /// <summary>Gets what the spawn produces.</summary>
    public ItemSpawnMode Mode { get; init; } = ItemSpawnMode.Loot;

    /// <summary>Gets the table to roll from; null uses the level's.</summary>
    public LootTableData? LootTable { get; init; }

    /// <summary>Gets tags an entry must carry to be rolled here; empty accepts any.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the probability the spawn produces anything at all.</summary>
    public float Chance { get; init; } = 1f;

    /// <summary>Gets how many items the spawn rolls.</summary>
    public int Count { get; init; } = 1;
}
