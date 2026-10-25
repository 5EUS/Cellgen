using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>Applies a fixed <see cref="RuleSet"/> to every generation, compiled afresh against each one's depth and unlock flags.</summary>
/// <remarks>Initializes a new instance of the <see cref="RulePolicySource"/> class.</remarks>
/// <param name="set">The rules.</param>
public sealed class RulePolicySource(RuleSet set) : IGenerationPolicySource
{
    private readonly RuleSet set = set;

    /// <inheritdoc/>
    public PolicyScope? Begin(LevelData def, GenContext context, GenReport report) =>
        RulePolicy.Create(this.set, context, report) is { } policy ? new PolicyScope(policy) : null;
}
