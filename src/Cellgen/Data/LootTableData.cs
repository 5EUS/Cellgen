namespace Cellgen;

/// <summary>What a level, an item spawn or a shop can roll.</summary>
public sealed class LootTableData
{
    /// <summary>Gets the stable identity, for diagnostics.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the pool this table also draws from; empty for a table of <see cref="Entries"/> only.</summary>
    public string Pool { get; init; } = string.Empty;

    /// <summary>Gets the hand-tuned entries, rolled alongside the pool's.</summary>
    public IReadOnlyList<LootEntryData> Entries { get; init; } = [];

    /// <summary>Gets everything the table can roll: its own entries, then the pool's.</summary>
    /// <param name="catalog">Where the pool's entries are found.</param>
    /// <returns>The entries, in a stable order.</returns>
    public IEnumerable<LootEntryData> Resolve(ILootCatalog catalog)
    {
        foreach (LootEntryData entry in this.Entries)
        {
            yield return entry;
        }

        if (string.IsNullOrEmpty(this.Pool))
        {
            yield break;
        }

        foreach (LootEntryData listing in catalog.Listings(this.Pool))
        {
            yield return listing;
        }
    }
}
