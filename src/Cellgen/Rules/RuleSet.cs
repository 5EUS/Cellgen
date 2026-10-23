using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>
/// Generation rules as data: the declarative form of an <see cref="IGenerationPolicy"/>.
/// Anything that can write JSON can produce one (a script, a data file, an editor),
/// and <see cref="RulePolicy"/> applies it natively, so rules cost a lookup per
/// candidate rather than a call into whatever produced them. The JSON form is the
/// contract: see <see cref="RuleSetReader"/>.
/// </summary>
public sealed class RuleSet
{
    /// <summary>Gets the schema version this library reads.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets the largest weight a rule may give; larger values are clamped to it.</summary>
    public const float MaxWeight = 1000f;

    /// <summary>Gets the smallest budget multiplier; smaller values are clamped to it.</summary>
    public const float MinBudgetScale = 0.1f;

    /// <summary>Gets the largest budget multiplier; larger values are clamped to it.</summary>
    public const float MaxBudgetScale = 10f;

    /// <summary>Gets or sets the schema version of this document. Must equal <see cref="CurrentVersion"/>.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the rules.</summary>
    public IReadOnlyList<Rule> Rules { get; set; } = [];

    /// <summary>Gets or sets the budget multipliers; null for none.</summary>
    public RuleBudgets? Budgets { get; set; }

    /// <summary>
    /// Checks a rule set and returns a clean copy: weights clamped, impossible rules
    /// dropped, each problem reported with the path of the field.
    /// </summary>
    /// <param name="diagnostics">One entry per problem; an error means the set as a whole is unusable.</param>
    /// <returns>The normalized set, or null when there was an error.</returns>
    public RuleSet? Normalize(out IReadOnlyList<GenDiagnostic> diagnostics) => RuleValidator.Normalize(this, out diagnostics);
}
