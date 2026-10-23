using Cellgen.Generation;
using Cellgen.Rules;

using Xunit;

namespace Cellgen.Tests;

/// <summary>The declarative rules: reading, validating, and what they do to a generation.</summary>
public sealed class RuleTests
{
    private static readonly string Everything = TestJson.Quote(
        "{ 'version': 1, 'rules': ["
        + "{ 'target': 'encounter', 'match': { 'tag': 'boss', 'max_depth': 2 }, 'allow': false },"
        + "{ 'target': 'cell', 'match': { 'id': 'mid_a.tscn' }, 'weight': 3.5 },"
        + "{ 'target': 'loot', 'match': { 'ids': ['item_a', 'item_b'], 'level': 'policy_test', 'unlock': 'x' }, 'weight': 0 }"
        + "], 'budgets': { 'encounter': 0.5, 'loot': 2 } }");

    /// <summary>A valid document is read into the rules it says.</summary>
    [Fact]
    public void ReadsADocument()
    {
        RuleParseResult result = RuleSetReader.Parse(Everything);

        RuleSet set = Assert.IsType<RuleSet>(result.Set);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, set.Rules.Count);
        Assert.Equal("boss", set.Rules[0].Match?.Tag);
        Assert.Equal(2, set.Rules[0].Match?.MaxDepth);
        Assert.False(set.Rules[0].Allow);
        Assert.Equal(3.5f, set.Rules[1].Weight);
        Assert.Equal(["item_a", "item_b"], set.Rules[2].Match?.Ids);
        Assert.Equal(0.5f, set.Budgets?.Encounter);
    }

    /// <summary>Writing and reading again gives the same rules.</summary>
    [Fact]
    public void RoundTrips()
    {
        RuleSet set = Assert.IsType<RuleSet>(RuleSetReader.Parse(Everything).Set);

        RuleSet again = Assert.IsType<RuleSet>(RuleSetReader.Parse(RuleSetReader.ToJson(set)).Set);

        Assert.Equal(RuleSetReader.ToJson(set), RuleSetReader.ToJson(again));
    }

    /// <summary>Structural problems reject the whole document, each with a reason.</summary>
    /// <param name="json">The document.</param>
    /// <param name="expected">Text the error contains.</param>
    [Theory]
    [InlineData("{ not json", "")]
    [InlineData("{ 'version': 2, 'rules': [] }", "version 2")]
    [InlineData("{ 'version': 1, 'rulez': [] }", "rulez")]
    [InlineData("null", "empty")]
    public void RejectsABrokenDocument(string json, string expected)
    {
        RuleParseResult result = RuleSetReader.Parse(TestJson.Quote(json));

        Assert.Null(result.Set);
        GenDiagnostic error = Assert.Single(result.Diagnostics);
        Assert.Equal(GenSeverity.Error, error.Severity);
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    /// <summary>A bad rule is dropped on its own with a warning; the good ones still apply.</summary>
    [Fact]
    public void DropsABadRuleAndKeepsTheRest()
    {
        string json = TestJson.Quote(
            "{ 'version': 1, 'rules': ["
            + "{ 'target': 'monster', 'allow': false },"
            + "{ 'target': 'cell', 'match': { 'id': 'a', 'id_prefix': 'b' }, 'allow': false },"
            + "{ 'target': 'loot', 'match': { 'tag': 'x' } },"
            + "{ 'target': 'structure', 'match': { 'tag': 'x' }, 'allow': false },"
            + "{ 'target': 'cell', 'match': { 'min_depth': 5, 'max_depth': 1 }, 'allow': false },"
            + "{ 'target': 'cell', 'match': { 'ids': [] }, 'allow': false },"
            + "{ 'target': 'encounter', 'match': { 'id': 'keep' }, 'allow': false }"
            + "] }");

        RuleParseResult result = RuleSetReader.Parse(json);

        RuleSet set = Assert.IsType<RuleSet>(result.Set);
        Rule kept = Assert.Single(set.Rules);
        Assert.Equal("keep", kept.Match?.Id);
        Assert.Equal(6, result.Diagnostics.Count(static d => d.Severity == GenSeverity.Warning));
    }

    /// <summary>Weights and budgets outside their range are clamped, and weights on things not drawn by weight are ignored.</summary>
    [Fact]
    public void ClampsNumbers()
    {
        string json = TestJson.Quote(
            "{ 'version': 1, 'rules': ["
            + "{ 'target': 'cell', 'weight': 99999 },"
            + "{ 'target': 'cell', 'match': { 'tag': 'a' }, 'weight': -3 },"
            + "{ 'target': 'prop', 'weight': 2, 'allow': false }"
            + "], 'budgets': { 'encounter': 100, 'loot': 0 } }");

        RuleSet set = Assert.IsType<RuleSet>(RuleSetReader.Parse(json).Set);

        Assert.Equal(RuleSet.MaxWeight, set.Rules[0].Weight);
        Assert.Equal(0f, set.Rules[1].Weight);
        Assert.Null(set.Rules[2].Weight);
        Assert.False(set.Rules[2].Allow);
        Assert.Equal(RuleSet.MaxBudgetScale, set.Budgets?.Encounter);
        Assert.Equal(RuleSet.MinBudgetScale, set.Budgets?.Loot);
    }

    /// <summary>A cell rule by id removes the cell from every generated level.</summary>
    [Fact]
    public void ACellRuleVetoesByIdAndWeighsByTag()
    {
        IReadOnlyList<IGenerationPolicySource> veto = Rules("{ 'version': 1, 'rules': [ { 'target': 'cell', 'match': { 'id': 'mid_b.tscn' }, 'allow': false } ] }");
        IReadOnlyList<IGenerationPolicySource> tagged = Rules("{ 'version': 1, 'rules': [ { 'target': 'cell', 'match': { 'id_prefix': 'mid_a' }, 'weight': 0 } ] }");

        for (ulong seed = 1; seed <= 15; seed++)
        {
            Assert.DoesNotContain(Plan(seed, sources: veto).Cells, static c => PolicyFixture.Same(c.Info.ScenePath, PolicyFixture.MidB));
            Assert.DoesNotContain(Plan(seed, sources: tagged).Cells, static c => PolicyFixture.Same(c.Info.ScenePath, PolicyFixture.MidA));
        }
    }

    /// <summary>Depth, unlock and level conditions decide whether a rule is in force for a generation.</summary>
    [Fact]
    public void ConditionsGateARule()
    {
        IReadOnlyList<IGenerationPolicySource> rules = Rules(@"            { 'version': 1, 'rules': [
              { 'target': 'encounter', 'match': { 'id': 'big', 'max_depth': 1 }, 'allow': false },
              { 'target': 'encounter', 'match': { 'id': 'small', 'unlock': 'no_small' }, 'allow': false },
              { 'target': 'encounter', 'match': { 'id': 'boss_one', 'level': 'some_other_level' }, 'allow': false }
            ] }");
        var unlock = new HashSet<string>(StringComparer.Ordinal) { "no_small" };

        IReadOnlyList<string> early = Encounters(rules, depth: 1);
        IReadOnlyList<string> late = Encounters(rules, depth: 2);
        IReadOnlyList<string> unlocked = Encounters(rules, depth: 2, unlocks: unlock);

        Assert.DoesNotContain("big", early);
        Assert.Contains("big", late);
        Assert.Contains("small", late);
        Assert.DoesNotContain("small", unlocked);
        Assert.Contains("boss_one", late);
    }

    /// <summary>Encounter, loot and prop rules each keep their targets out; structure and budget rules work too.</summary>
    [Fact]
    public void RulesReachEveryTarget()
    {
        IReadOnlyList<IGenerationPolicySource> rules = Rules(@"            { 'version': 1, 'rules': [
              { 'target': 'encounter', 'match': { 'tag': 'boss' }, 'allow': false },
              { 'target': 'loot', 'match': { 'id': 'item_b' }, 'allow': false },
              { 'target': 'prop', 'match': { 'id': 'prop_b' }, 'allow': false },
              { 'target': 'structure', 'match': { 'id': 'shop' }, 'allow': false }
            ], 'budgets': { 'encounter': 0.5, 'loot': 2 } }");

        for (ulong seed = 1; seed <= 12; seed++)
        {
            LevelPlan plan = Plan(seed, sources: rules);
            Assert.DoesNotContain(plan.Zones, static z => PolicyFixture.Same(z.EncounterId, "boss_one"));
            Assert.DoesNotContain(plan.Items, static i => PolicyFixture.Same(i.ItemId, "item_b"));
            Assert.DoesNotContain(plan.Props, static p => PolicyFixture.Same(p.PropId, "prop_b"));
            Assert.Equal(Plan(seed).EncounterBudget * 0.5f, plan.EncounterBudget);
            Assert.Equal(Plan(seed).LootBudget * 2f, plan.LootBudget);
        }

        var shop = new StructureData { Id = "shop", Requirement = StructureRequirement.Required };
        LevelData recipe = PolicyFixture.Recipe();
        GenContext context = PolicyFixture.ContextFor(1, sources: rules);
        var report = new GenReport();
        IGenerationPolicy policy = Assert.IsType<PolicyScope>(rules[0].Begin(recipe, context, report)).Policies[0];
        Assert.Equal(0, StructurePlacer.CountFor(recipe, shop, new GenRng(1), PolicyFixture.ContextWith(policy)));
    }

    /// <summary>An empty rule set changes nothing.</summary>
    [Fact]
    public void AnEmptyRuleSetIsInert()
    {
        IReadOnlyList<IGenerationPolicySource> none = Rules("{ 'version': 1 }");

        for (ulong seed = 1; seed <= 10; seed++)
        {
            Assert.Equal(Plan(seed).Fingerprint(), Plan(seed, sources: none).Fingerprint());
        }
    }

    /// <summary>A source is asked once per generation and its policy is disposed when it ends, however many seeds a sweep runs.</summary>
    [Fact]
    public void SourcesAreRequestScoped()
    {
        var source = new CountingSource();
        GenContext template = PolicyFixture.ContextFor(1, sources: [source]);

        LevelStats stats = LevelStats.Sweep(PolicyFixture.Recipe(), template, count: 6);

        Assert.Empty(stats.Failures);
        Assert.True(source.Begun >= 6, $"begun {source.Begun}");
        Assert.Equal(source.Begun, source.Ended);
    }

    /// <summary>A scope may carry several policies, applied in order.</summary>
    [Fact]
    public void AScopeCarriesSeveralPolicies()
    {
        var both = new PolicyScope([new VetoCellMidB(), new VetoCellMidA()]);
        var source = new FixedSource(both);

        for (ulong seed = 1; seed <= 10; seed++)
        {
            GenResult result = LevelGenerator.Generate(PolicyFixture.Recipe(), PolicyFixture.ContextFor(seed, sources: [source]));
            Assert.Null(result.Plan);
        }
    }

    /// <summary>A failed recipe falls back to another, which begins its own policies; the first's are already disposed.</summary>
    [Fact]
    public void TheFallbackRecipeBeginsAgain()
    {
        LevelData fallback = PolicyFixture.Recipe();
        LevelData broken = new() { Id = "broken", LayoutId = "does_not_exist", Fallback = fallback };
        var source = new CountingSource();

        GenResult result = LevelGenerator.Generate(broken, PolicyFixture.ContextFor(1, sources: [source]));

        Assert.NotNull(result.Plan);
        Assert.Equal(2, source.Begun);
        Assert.Equal(2, source.Ended);
    }

    /// <summary>Facts the host puts on the context reach a source, and survive a copy.</summary>
    [Fact]
    public void FactsReachSources()
    {
        var facts = new Dictionary<string, object>(StringComparer.Ordinal) { ["preset"] = "hard" };
        var source = new CountingSource();

        _ = LevelGenerator.Generate(PolicyFixture.Recipe(), PolicyFixture.ContextFor(1, sources: [source], facts: facts));

        Assert.Equal("hard", source.LastPreset);
        Assert.Equal("hard", PolicyFixture.ContextFor(1, facts: facts).With(2, 0).Facts["preset"]);
    }

    private static IReadOnlyList<IGenerationPolicySource> Rules(string json)
    {
        RuleParseResult parsed = RuleSetReader.Parse(TestJson.Quote(json));
        Assert.Empty(parsed.Diagnostics);
        return [new RulePolicySource(Assert.IsType<RuleSet>(parsed.Set))];
    }

    private static LevelPlan Plan(ulong seed, int depth = 0, IReadOnlyList<IGenerationPolicySource>? sources = null, IReadOnlySet<string>? unlocks = null) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(PolicyFixture.Recipe(), PolicyFixture.ContextFor(seed, depth, sources, unlocks)).Plan);

    private static List<string> Encounters(IReadOnlyList<IGenerationPolicySource> sources, int depth, IReadOnlySet<string>? unlocks = null) =>
        [.. Enumerable.Range(1, 30).SelectMany(seed => Plan((ulong)seed, depth, sources, unlocks).Zones.Select(static z => z.EncounterId))];

    private sealed class FixedSource(PolicyScope scope) : IGenerationPolicySource
    {
        public PolicyScope? Begin(LevelData def, GenContext context, GenReport report) => scope;
    }

    private sealed class VetoCellMidA : IGenerationPolicy
    {
        public bool AllowCell(in CellQuery query) => !PolicyFixture.Same(query.Cell.ScenePath, PolicyFixture.MidA);
    }

    private sealed class VetoCellMidB : IGenerationPolicy
    {
        public bool AllowCell(in CellQuery query) => !PolicyFixture.Same(query.Cell.ScenePath, PolicyFixture.MidB);
    }

    private sealed class CountingSource : IGenerationPolicySource
    {
        public int Begun { get; private set; }

        public int Ended { get; private set; }

        public string? LastPreset { get; private set; }

        public PolicyScope? Begin(LevelData def, GenContext context, GenReport report)
        {
            this.Begun++;
            this.LastPreset = context.Facts.GetValueOrDefault("preset") as string;
            return new PolicyScope(new InertPolicy(), () => this.Ended++);
        }

        private sealed class InertPolicy : IGenerationPolicy
        {
        }
    }
}
