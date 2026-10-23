namespace Cellgen.Generation;

/// <summary>Combines the policies in a <see cref="GenContext"/>: vetoes AND together, weights and scales multiply.</summary>
internal static class PolicyEval
{
    /// <summary>Gets whether every policy lets the solver place a cell here.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>True unless a policy vetoes it.</returns>
    internal static bool AllowCell(GenContext context, in CellQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        for (int i = 0; i < policies.Count; i++)
        {
            if (!policies[i].AllowCell(query))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the combined policy multiplier for a cell here.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>The product of the policies' multipliers; one when none has a say.</returns>
    internal static float CellWeight(GenContext context, in CellQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        float weight = 1f;
        for (int i = 0; i < policies.Count; i++)
        {
            weight *= policies[i].CellWeight(query);
        }

        return weight;
    }

    /// <summary>Gets the combined weight of an encounter for a zone.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>Zero when vetoed or weighted out, one when no policy has a say.</returns>
    internal static float EncounterWeight(GenContext context, in EncounterQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        float weight = 1f;
        for (int i = 0; i < policies.Count; i++)
        {
            if (!policies[i].AllowEncounter(query))
            {
                return 0f;
            }

            weight *= policies[i].EncounterWeight(query);
        }

        return weight;
    }

    /// <summary>Gets the combined weight of a loot entry for a spawn.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>Zero when vetoed or weighted out, one when no policy has a say.</returns>
    internal static float LootWeight(GenContext context, in LootQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        float weight = 1f;
        for (int i = 0; i < policies.Count; i++)
        {
            if (!policies[i].AllowLoot(query))
            {
                return 0f;
            }

            weight *= policies[i].LootWeight(query);
        }

        return weight;
    }

    /// <summary>Gets whether every policy lets a spawn place a prop.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>True unless a policy vetoes it.</returns>
    internal static bool AllowProp(GenContext context, in PropQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        for (int i = 0; i < policies.Count; i++)
        {
            if (!policies[i].AllowProp(query))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether every policy lets a structure be placed.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="query">The question asked.</param>
    /// <returns>True unless a policy vetoes it.</returns>
    internal static bool AllowStructure(GenContext context, in StructureQuery query)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        for (int i = 0; i < policies.Count; i++)
        {
            if (!policies[i].AllowStructure(query))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the combined multiplier for the encounter budget.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth and policies.</param>
    /// <returns>The product of the policies' scales.</returns>
    internal static float EncounterBudgetScale(LevelData def, GenContext context)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        float scale = 1f;
        for (int i = 0; i < policies.Count; i++)
        {
            scale *= policies[i].EncounterBudgetScale(def, context);
        }

        return scale;
    }

    /// <summary>Gets the combined multiplier for the loot budget.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth and policies.</param>
    /// <returns>The product of the policies' scales.</returns>
    internal static float LootBudgetScale(LevelData def, GenContext context)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        float scale = 1f;
        for (int i = 0; i < policies.Count; i++)
        {
            scale *= policies[i].LootBudgetScale(def, context);
        }

        return scale;
    }

    /// <summary>Asks the policies whether a finished plan is acceptable.</summary>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="plan">The finished plan.</param>
    /// <returns>The first policy's reason for refusing it, or null.</returns>
    internal static string? RejectPlan(GenContext context, LevelPlan plan)
    {
        IReadOnlyList<IGenerationPolicy> policies = context.Policies;
        for (int i = 0; i < policies.Count; i++)
        {
            if (policies[i].RejectPlan(plan) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }
}
