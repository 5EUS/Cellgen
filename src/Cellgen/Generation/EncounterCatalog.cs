namespace Cellgen.Generation;

/// <summary>
/// The encounters content defines, as the populator needs them: id, cost,
/// tags, depth range, and the run's budget growth. Built from the live
/// extension in game and from the content manifest in the editor - both
/// through <c>LevelBridge</c>, which owns the names involved.
/// </summary>
/// <param name="entries">The encounters, sorted by id.</param>
/// <param name="budgetGrowth">Per-depth growth of a level's encounter budget.</param>
public sealed class EncounterCatalog(IReadOnlyList<EncounterEntry> entries, float budgetGrowth)
{
    /// <summary>Gets a catalog with no encounters: zones stay empty.</summary>
    public static EncounterCatalog Empty { get; } = new([], 1f);

    /// <summary>Gets the encounters.</summary>
    public IReadOnlyList<EncounterEntry> Entries { get; } = entries;

    /// <summary>Gets the per-depth budget growth.</summary>
    public float BudgetGrowth { get; } = budgetGrowth;
}
