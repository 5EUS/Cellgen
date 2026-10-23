namespace Cellgen;

/// <summary>One possible roll in a <see cref="LootTableData"/>.</summary>
public sealed class LootEntryData
{
    /// <summary>Gets the item's id. The generator never looks inside it.</summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>Gets how likely the entry is relative to the others that fit.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Gets what the entry spends from a level's loot budget, or its shop price.</summary>
    public float Cost { get; init; } = 1f;

    /// <summary>Gets the first run depth the entry may roll at.</summary>
    public int MinDepth { get; init; }

    /// <summary>Gets the last run depth; -1 for no limit.</summary>
    public int MaxDepth { get; init; } = -1;

    /// <summary>Gets tags an item spawn can filter on.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
