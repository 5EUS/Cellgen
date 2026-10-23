namespace Cellgen.Generation;

/// <summary>Everything a level depends on besides its recipe.</summary>
public sealed class GenContext
{
    /// <summary>Gets the level's seed.</summary>
    public ulong Seed { get; init; }

    /// <summary>Gets the run depth, from 0.</summary>
    public int Depth { get; init; }

    /// <summary>Gets the meta-progression flags set so far, which unlock structures.</summary>
    public IReadOnlySet<string> UnlockFlags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the encounters zones can be given.</summary>
    public EncounterCatalog Encounters { get; init; } = EncounterCatalog.Empty;

    /// <summary>Gets where loot tables find the entries of the pools they draw from.</summary>
    public ILootCatalog Loot { get; init; } = ILootCatalog.Empty;
}
