using Xunit;

namespace Cellgen.Tests;

/// <summary>Tests for the plain-data model the generator reads.</summary>
public sealed class DataTests
{
    /// <summary>A type always matches itself.</summary>
    [Fact]
    public void SocketAcceptsItself()
    {
        var trench = new SocketData { Id = "trench" };
        Assert.True(trench.Accepts(new SocketData { Id = "trench" }));
    }

    /// <summary>Compatibility is symmetric: one side listing the other is enough.</summary>
    [Fact]
    public void SocketCompatibilityIsSymmetric()
    {
        var open = new SocketData { Id = "field_open" };
        var edge = new SocketData { Id = "edge", CompatibleWith = [open] };
        Assert.True(edge.Accepts(open));
        Assert.True(open.Accepts(edge));
        Assert.False(open.Accepts(new SocketData { Id = "tunnel" }));
    }

    /// <summary>A table's own entries come first, then its pool's.</summary>
    [Fact]
    public void LootTableResolvesEntriesThenPool()
    {
        var table = new LootTableData
        {
            Pool = "common",
            Entries = [new LootEntryData { ItemId = "hand_picked" }],
        };

        string[] ids = [.. table.Resolve(new FixedCatalog()).Select(static e => e.ItemId)];
        Assert.Equal(["hand_picked", "pooled_a", "pooled_b"], ids);
    }

    /// <summary>A table with no pool never asks the catalog.</summary>
    [Fact]
    public void LootTableWithoutPoolIgnoresTheCatalog()
    {
        var table = new LootTableData { Entries = [new LootEntryData { ItemId = "only" }] };
        Assert.Single(table.Resolve(new FixedCatalog()));
    }

    private sealed class FixedCatalog : ILootCatalog
    {
        public IEnumerable<LootEntryData> Listings(string pool) =>
            [new LootEntryData { ItemId = "pooled_a" }, new LootEntryData { ItemId = "pooled_b" }];
    }
}
