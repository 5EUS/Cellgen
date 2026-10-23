namespace Cellgen.Rules;

/// <summary>Multipliers on a level's budgets, applied after depth scaling. Null leaves a budget alone.</summary>
public sealed class RuleBudgets
{
    /// <summary>Gets or sets the multiplier on the encounter budget.</summary>
    public float? Encounter { get; set; }

    /// <summary>Gets or sets the multiplier on the loot budget.</summary>
    public float? Loot { get; set; }
}
