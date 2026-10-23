namespace Cellgen;

/// <summary>Where a loot table's pool finds its entries: the host's items, however it stores them.</summary>
public interface ILootCatalog
{
    /// <summary>Gets an empty catalog, for tables that list all their entries by hand.</summary>
    static ILootCatalog Empty { get; } = new EmptyLootCatalog();

    /// <summary>Gets every entry that lists itself in <paramref name="pool"/>, in a stable order.</summary>
    /// <param name="pool">The pool's name.</param>
    /// <returns>The entries.</returns>
    IEnumerable<LootEntryData> Listings(string pool);

    /// <summary>A catalog with no pools.</summary>
    private sealed class EmptyLootCatalog : ILootCatalog
    {
        /// <inheritdoc/>
        public IEnumerable<LootEntryData> Listings(string pool) => [];
    }
}
