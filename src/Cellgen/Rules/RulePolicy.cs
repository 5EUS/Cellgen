using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>
/// A <see cref="RuleSet"/> compiled against one generation: rules whose depth, unlock
/// or level conditions cannot hold are dropped up front, and the rest are indexed by id
/// and target, so answering a query costs a lookup and a few tag checks, never a call
/// into whatever wrote the rules.
/// </summary>
public sealed class RulePolicy : IGenerationPolicy
{
    private readonly Bucket cells = new();
    private readonly Bucket encounters = new();
    private readonly Bucket loot = new();
    private readonly Bucket props = new();
    private readonly Bucket structures = new();
    private readonly float encounterScale;
    private readonly float lootScale;

    private RulePolicy(RuleSet set, GenContext context)
    {
        foreach (Rule rule in set.Rules)
        {
            Compile(rule, context);
        }

        this.encounterScale = set.Budgets?.Encounter ?? 1f;
        this.lootScale = set.Budgets?.Loot ?? 1f;

        void Compile(Rule rule, GenContext generation)
        {
            RuleMatch match = rule.Match ?? new RuleMatch();
            if ((match.MinDepth is { } min && generation.Depth < min)
                || (match.MaxDepth is { } max && generation.Depth > max)
                || (match.Unlock is { } flag && !generation.UnlockFlags.Contains(flag, StringComparer.Ordinal)))
            {
                return;
            }

            var compiled = new CompiledRule(match, rule.Allow == false, rule.Weight ?? 1f);
            Bucket bucket = this.BucketFor(rule.Target);
            if (match.Id is not null)
            {
                bucket.Index(match.Id, compiled);
            }
            else if (match.Ids is not null)
            {
                foreach (string id in match.Ids)
                {
                    bucket.Index(id, compiled);
                }
            }
            else
            {
                bucket.Scan.Add(compiled);
            }
        }
    }

    /// <summary>Compiles a rule set for one generation.</summary>
    /// <param name="set">The rule set, as written or already normalized.</param>
    /// <param name="context">The depth and unlock flags the conditions are resolved against.</param>
    /// <param name="report">Receives a warning per problem in the set; may be null.</param>
    /// <returns>The policy, or null when the set was unusable as a whole.</returns>
    public static RulePolicy? Create(RuleSet set, GenContext context, GenReport? report = null)
    {
        RuleSet? normalized = set.Normalize(out IReadOnlyList<GenDiagnostic> diagnostics);
        if (report is not null)
        {
            foreach (GenDiagnostic diagnostic in diagnostics)
            {
                report.Warn($"rules.{diagnostic.Path}", diagnostic.Message);
            }
        }

        return (normalized is null) ? null : new RulePolicy(normalized, context);
    }

    /// <inheritdoc/>
    public bool AllowCell(in CellQuery query) => !Veto(this.cells, query.Cell.ScenePath, query.Cell.Tags, query.Def.Id);

    /// <inheritdoc/>
    public float CellWeight(in CellQuery query) => Weight(this.cells, query.Cell.ScenePath, query.Cell.Tags, query.Def.Id);

    /// <inheritdoc/>
    public bool AllowEncounter(in EncounterQuery query) => !Veto(this.encounters, query.Encounter.Id, query.Encounter.Tags, query.Def.Id);

    /// <inheritdoc/>
    public float EncounterWeight(in EncounterQuery query) => Weight(this.encounters, query.Encounter.Id, query.Encounter.Tags, query.Def.Id);

    /// <inheritdoc/>
    public float EncounterBudgetScale(LevelData def, GenContext context) => this.encounterScale;

    /// <inheritdoc/>
    public bool AllowLoot(in LootQuery query) => !Veto(this.loot, query.Entry.ItemId, query.Entry.Tags, query.Def.Id);

    /// <inheritdoc/>
    public float LootWeight(in LootQuery query) => Weight(this.loot, query.Entry.ItemId, query.Entry.Tags, query.Def.Id);

    /// <inheritdoc/>
    public float LootBudgetScale(LevelData def, GenContext context) => this.lootScale;

    /// <inheritdoc/>
    public bool AllowProp(in PropQuery query) => !Veto(this.props, query.PropId, query.Cell.Info.Tags, query.Def.Id);

    /// <inheritdoc/>
    public bool AllowStructure(in StructureQuery query) => !Veto(this.structures, query.Structure.Id, [], query.Def.Id);

    private static bool Applies(CompiledRule rule, string id, IReadOnlyList<string> tags, string level)
    {
        if (rule.Level is not null && !string.Equals(rule.Level, level, StringComparison.Ordinal))
        {
            return false;
        }

        if (rule.Prefix is not null && !id.StartsWith(rule.Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (rule.Tag is not null && !Carries(tags, rule.Tag))
        {
            return false;
        }

        if (rule.TagsAny is not null && !rule.TagsAny.Any(tag => Carries(tags, tag)))
        {
            return false;
        }

        return rule.TagsAll is null || rule.TagsAll.All(tag => Carries(tags, tag));
    }

    private static bool Carries(IReadOnlyList<string> tags, string tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Veto(Bucket bucket, string id, IReadOnlyList<string> tags, string level)
    {
        if (bucket.IsEmpty)
        {
            return false;
        }

        if (bucket.ById.TryGetValue(id, out List<CompiledRule>? indexed))
        {
            foreach (CompiledRule rule in indexed)
            {
                if (rule.Vetoes && Applies(rule, id, tags, level))
                {
                    return true;
                }
            }
        }

        foreach (CompiledRule rule in bucket.Scan)
        {
            if (rule.Vetoes && Applies(rule, id, tags, level))
            {
                return true;
            }
        }

        return false;
    }

    private static float Weight(Bucket bucket, string id, IReadOnlyList<string> tags, string level)
    {
        if (bucket.IsEmpty)
        {
            return 1f;
        }

        float weight = 1f;
        if (bucket.ById.TryGetValue(id, out List<CompiledRule>? indexed))
        {
            foreach (CompiledRule rule in indexed)
            {
                weight *= Applies(rule, id, tags, level) ? rule.Multiplier : 1f;
            }
        }

        foreach (CompiledRule rule in bucket.Scan)
        {
            weight *= Applies(rule, id, tags, level) ? rule.Multiplier : 1f;
        }

        return weight;
    }

    private Bucket BucketFor(string target) => target switch
    {
        RuleTargets.Cell => this.cells,
        RuleTargets.Encounter => this.encounters,
        RuleTargets.Loot => this.loot,
        RuleTargets.Prop => this.props,
        _ => this.structures,
    };

    private sealed class Bucket
    {
        public Dictionary<string, List<CompiledRule>> ById { get; } = new(StringComparer.Ordinal);

        public List<CompiledRule> Scan { get; } = [];

        public bool IsEmpty => this.ById.Count == 0 && this.Scan.Count == 0;

        public void Index(string id, CompiledRule rule)
        {
            if (!this.ById.TryGetValue(id, out List<CompiledRule>? list))
            {
                list = [];
                this.ById[id] = list;
            }

            list.Add(rule);
        }
    }

    private sealed class CompiledRule(RuleMatch match, bool veto, float weight)
    {
        public string? Level { get; } = match.Level;

        public string? Prefix { get; } = match.IdPrefix;

        public string? Tag { get; } = match.Tag;

        public IReadOnlyList<string>? TagsAny { get; } = match.TagsAny;

        public IReadOnlyList<string>? TagsAll { get; } = match.TagsAll;

        public bool Vetoes { get; } = veto;

        public float Multiplier { get; } = weight;
    }
}
