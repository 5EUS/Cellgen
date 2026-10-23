namespace Cellgen.Rules;

/// <summary>
/// One rule: for things of a <see cref="Target"/> that satisfy <see cref="Match"/>, forbid
/// them, or scale how likely they are. Rules are not ordered: every rule that matches
/// applies, a veto from any stands and weights multiply.
/// </summary>
public sealed class Rule
{
    /// <summary>Gets or sets what the rule is about; one of <see cref="RuleTargets"/>.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Gets or sets which of that kind it applies to; null for all.</summary>
    public RuleMatch? Match { get; set; }

    /// <summary>Gets or sets <c>false</c> to forbid what matches. <c>true</c> or null changes nothing.</summary>
    public bool? Allow { get; set; }

    /// <summary>Gets or sets a multiplier on how likely what matches is to be chosen, from 0 (never) to <see cref="RuleSet.MaxWeight"/>. Not meaningful for props and structures, which are not drawn by weight.</summary>
    public float? Weight { get; set; }
}
