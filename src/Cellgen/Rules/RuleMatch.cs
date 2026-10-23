namespace Cellgen.Rules;

/// <summary>
/// Which things a <see cref="Rule"/> applies to. Every field that is set must hold;
/// a match with no fields set matches everything of the rule's target. At most one
/// of <see cref="Id"/>, <see cref="Ids"/> and <see cref="IdPrefix"/> may be set.
/// </summary>
public sealed class RuleMatch
{
    /// <summary>Gets or sets the exact id.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets a list of ids, any of which matches.</summary>
    public IReadOnlyList<string>? Ids { get; set; }

    /// <summary>Gets or sets a prefix the id must start with, such as a mod's <c>modid:</c>.</summary>
    public string? IdPrefix { get; set; }

    /// <summary>Gets or sets a tag the thing must carry.</summary>
    public string? Tag { get; set; }

    /// <summary>Gets or sets tags of which the thing must carry at least one.</summary>
    public IReadOnlyList<string>? TagsAny { get; set; }

    /// <summary>Gets or sets tags the thing must carry all of.</summary>
    public IReadOnlyList<string>? TagsAll { get; set; }

    /// <summary>Gets or sets the first run depth the rule applies at.</summary>
    public int? MinDepth { get; set; }

    /// <summary>Gets or sets the last run depth the rule applies at.</summary>
    public int? MaxDepth { get; set; }

    /// <summary>Gets or sets the id of the recipe the rule applies to.</summary>
    public string? Level { get; set; }

    /// <summary>Gets or sets an unlock flag that must be set for the rule to apply.</summary>
    public string? Unlock { get; set; }
}
