namespace Cellgen.Rules;

/// <summary>The kinds of thing a <see cref="Rule"/> can be about; the values of <see cref="Rule.Target"/>.</summary>
public static class RuleTargets
{
    /// <summary>Gets the target for cells the solver places. Matches by scene path and the cell's tags.</summary>
    public const string Cell = "cell";

    /// <summary>Gets the target for encounters given to spawn zones. Matches by encounter id and tags.</summary>
    public const string Encounter = "encounter";

    /// <summary>Gets the target for loot entries rolled by item spawns. Matches by item id and the entry's tags.</summary>
    public const string Loot = "loot";

    /// <summary>Gets the target for props placed on prop spawns. Matches by prop id and the tags of the cell the spawn is in.</summary>
    public const string Prop = "prop";

    /// <summary>Gets the target for structures. Matches by structure id; structures have no tags.</summary>
    public const string Structure = "structure";

    /// <summary>Gets every target.</summary>
    public static IReadOnlyList<string> All { get; } = [Cell, Encounter, Loot, Prop, Structure];
}
