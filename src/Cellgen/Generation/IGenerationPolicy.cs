namespace Cellgen.Generation;

/// <summary>
/// Rules a host or a mod layers over generation without replacing any of it.
/// Cellgen keeps the mechanism (the weighted draw, the budgets, the solver); a
/// policy says what may be drawn and how much it counts. Every member has a
/// default that changes nothing, so a policy implements only what it needs.
/// Policies are listed in <see cref="GenContext.Policies"/>.
///
/// <para>
/// Hooks take data in and give data out. A policy is never handed a random
/// stream and must not use one of its own: with no policy installed the output
/// is exactly what it was before policies existed, and a policy that is a pure
/// function of its query keeps generation deterministic. Answers about cells
/// (<see cref="AllowCell"/>, <see cref="CellWeight"/>) may be cached by the
/// solver, so they must depend on the query alone, not on what else was placed.
/// </para>
///
/// <para>
/// With several policies a veto from any one stands, weights multiply, and a
/// weight of zero or less removes the candidate. A policy that throws aborts the
/// generation: the exception is the host's to handle.
/// </para>
/// </summary>
public interface IGenerationPolicy
{
    /// <summary>Gets whether the solver may place a cell here.</summary>
    /// <param name="query">The cell and where it would go.</param>
    /// <returns>False to rule it out.</returns>
    bool AllowCell(in CellQuery query) => true;

    /// <summary>Gets how much more or less likely a cell is to be tried here, as a multiplier of its own weight.</summary>
    /// <param name="query">The cell and where it would go.</param>
    /// <returns>The multiplier; one changes nothing.</returns>
    float CellWeight(in CellQuery query) => 1f;

    /// <summary>Gets whether a spawn zone may be given an encounter.</summary>
    /// <param name="query">The encounter and the zone.</param>
    /// <returns>False to rule it out.</returns>
    bool AllowEncounter(in EncounterQuery query) => true;

    /// <summary>Gets how much more or less likely a zone is to be given an encounter, as a multiplier.</summary>
    /// <param name="query">The encounter and the zone.</param>
    /// <returns>The multiplier; one changes nothing.</returns>
    float EncounterWeight(in EncounterQuery query) => 1f;

    /// <summary>Gets a multiplier for the level's encounter budget, after depth scaling.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth, unlock flags and catalogs.</param>
    /// <returns>The multiplier; one changes nothing.</returns>
    float EncounterBudgetScale(LevelData def, GenContext context) => 1f;

    /// <summary>Gets whether an item spawn may roll a loot entry.</summary>
    /// <param name="query">The entry and the spawn.</param>
    /// <returns>False to rule it out.</returns>
    bool AllowLoot(in LootQuery query) => true;

    /// <summary>Gets how much more or less likely an item spawn is to roll an entry, as a multiplier of its own weight.</summary>
    /// <param name="query">The entry and the spawn.</param>
    /// <returns>The multiplier; one changes nothing.</returns>
    float LootWeight(in LootQuery query) => 1f;

    /// <summary>Gets a multiplier for the level's loot budget, after depth scaling.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth, unlock flags and catalogs.</param>
    /// <returns>The multiplier; one changes nothing.</returns>
    float LootBudgetScale(LevelData def, GenContext context) => 1f;

    /// <summary>Gets whether a prop spawn may place a prop.</summary>
    /// <param name="query">The prop and the spawn.</param>
    /// <returns>False to rule it out.</returns>
    bool AllowProp(in PropQuery query) => true;

    /// <summary>Gets whether a structure may be placed in this level at all.</summary>
    /// <param name="query">The structure.</param>
    /// <returns>False to place none of it.</returns>
    bool AllowStructure(in StructureQuery query) => true;

    /// <summary>Looks over a finished plan and may refuse it, which fails the attempt so the generator retries on a derived seed.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>Why it is refused, or null to accept.</returns>
    string? RejectPlan(LevelPlan plan) => null;
}
