using System.Text.Json.Nodes;

using Cellgen.Generation;
using Cellgen.Interchange;
using Cellgen.Rules;

using Xunit;

namespace Cellgen.Tests;

/// <summary>The language-free contracts: plan descriptions, the requests scripts receive, and merged rule sets.</summary>
public sealed class InterchangeTests
{
    private const string Line = "{ 'version': 1, "
        + "'slots': [ { 'at': [0,0,0], 'role': 'start' }, { 'at': [1,0,0], 'role': 'path' }, { 'at': [2,0,0], 'role': 'path' }, { 'at': [3,0,0], 'role': 'exit' } ], "
        + "'path': [ [0,0,0], [1,0,0], [2,0,0], [3,0,0] ] }";

    /// <summary>A described layout builds the same level as the native planner with the same topology.</summary>
    [Fact]
    public void ADescribedLayoutEqualsTheNativeOne()
    {
        var layouts = LayoutRegistry.CreateBuiltin();
        layouts.Register(new PolicyFixture.FourInALine());
        layouts.Register(new DescribedLayoutPlanner("described", _ => TestJson.Quote(Line)));

        for (ulong seed = 1; seed <= 12; seed++)
        {
            GenContext context = new() { Seed = seed, Layouts = layouts, Encounters = PolicyFixture.Context(seed).Encounters };
            string native = Fingerprint(PolicyFixture.Recipe(), context);
            string described = Fingerprint(PolicyFixture.Recipe(layoutId: "described"), context);

            Assert.Equal(native, described);
        }
    }

    /// <summary>Everything wrong with a description is reported, by path, and fails the attempt instead of corrupting it.</summary>
    /// <param name="json">The description, with single quotes.</param>
    /// <param name="expected">Text the failure contains.</param>
    [Theory]
    [InlineData("{ not json", "not valid")]
    [InlineData("{ 'version': 2 }", "version 2")]
    [InlineData("{ 'version': 1, 'slotz': [] }", "slotz")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [9,0,0], 'role': 'start' } ] }", "outside the 8x8 grid")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0], 'role': 'start' } ] }", "[x, y, z]")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'boss' } ] }", "not start, path, branch or exit")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'start' }, { 'at': [0,0,0], 'role': 'path' } ] }", "listed twice")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'start' } ], 'path': [ [0,0,0] ] }", "at least a start and an exit")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'start' }, { 'at': [2,0,0], 'role': 'exit' } ], 'path': [ [0,0,0], [2,0,0] ] }", "not next to")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'path' }, { 'at': [1,0,0], 'role': 'exit' } ], 'path': [ [0,0,0], [1,0,0] ] }", "must be a slot with role start")]
    [InlineData("{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'start' }, { 'at': [1,0,0], 'role': 'exit' }, { 'at': [4,0,0], 'role': 'path' } ], 'path': [ [0,0,0], [1,0,0] ] }", "not on the path")]
    [InlineData("{ 'version': 1, 'links': [ { 'a': [0,0,0], 'b': [1,0,0] } ] }", "must be listed in slots")]
    [InlineData("{ 'version': 1, 'require_tag': [ { 'at': [5,0,0], 'tag': 'x' } ] }", "is not listed in slots")]
    [InlineData("{ 'version': 1, 'structures': [ { 'index': 9 } ] }", "not one of the recipe's 0 structures")]
    public void InvalidDescriptionsFailWithAReason(string json, string expected)
    {
        MacroPlan? plan = PlanFrom(json, PolicyFixture.Recipe(), out string failure);

        Assert.Null(plan);
        Assert.Contains(expected, failure, StringComparison.Ordinal);
    }

    /// <summary>The described plan carries over roles, links, tags and sockets.</summary>
    [Fact]
    public void ADescriptionBuildsTheWholePlan()
    {
        string json = "{ 'version': 1, 'slots': [ { 'at': [0,0,0], 'role': 'start' }, { 'at': [1,0,0], 'role': 'exit' }, { 'at': [0,0,1], 'role': 'branch' } ], "
            + "'path': [ [0,0,0], [1,0,0] ], 'links': [ { 'a': [0,0,0], 'b': [0,0,1] } ], "
            + "'require_tag': [ { 'at': [0,0,1], 'tag': 'dead_end' } ], 'require_socket': [ { 'at': [0,0,1], 'face': 'north', 'socket': 'path' } ] }";

        MacroPlan plan = Assert.IsType<MacroPlan>(PlanFrom(json, PolicyFixture.Recipe(), out _));

        Assert.Equal(2, plan.PathLength);
        Assert.Equal(new Int3(0, 0, 0), plan.Start);
        Assert.Equal(new Int3(1, 0, 0), plan.Exit);
        Assert.True(plan.IsLinked(new Int3(0, 0, 0), new Int3(1, 0, 0)));
        Assert.True(plan.IsLinked(new Int3(0, 0, 1), new Int3(0, 0, 0)));
        Assert.True(plan.TryGetRole(new Int3(0, 0, 1), out SlotRole role) && role == SlotRole.Branch);
        Assert.Equal("dead_end", plan.RequiredTags[new Int3(0, 0, 1)]);
        Assert.Equal("path", plan.RequiredSockets[(new Int3(0, 0, 1), CellFace.North)]);
    }

    /// <summary>A structure is placed natively inside the region it is given, and a required one that finds no room fails the layout.</summary>
    [Fact]
    public void StructuresArePlacedInTheirRegion()
    {
        LevelData recipe = RecipeWithShop(StructureRequirement.Required);
        string row = "'slots': [ { 'at': [1,0,2], 'role': 'start' }, { 'at': [2,0,2], 'role': 'path' }, { 'at': [3,0,2], 'role': 'exit' } ], 'path': [ [1,0,2], [2,0,2], [3,0,2] ]";

        MacroPlan placed = Assert.IsType<MacroPlan>(PlanFrom($"{{ 'version': 1, {row}, 'structures': [ {{ 'index': 1, 'count': 1, 'region': {{ 'usable': {{ 'x': [1,3], 'z': [3,4] }} }} }} ] }}", recipe, out _));
        Assert.Single(placed.Structures);
        Assert.All(placed.Structures[0].Pieces, static p => Assert.InRange(p.Anchor.Z, 3, 4));

        Assert.Null(PlanFrom($"{{ 'version': 1, {row}, 'structures': [ {{ 'index': 1, 'count': 1, 'region': {{ 'usable': {{ 'x': [1,3], 'z': [0,0] }} }} }} ] }}", recipe, out string cramped));
        Assert.Contains("required but the layout did not place it", cramped, StringComparison.Ordinal);

        Assert.Null(PlanFrom($"{{ 'version': 1, {row} }}", recipe, out string forgotten));
        Assert.Contains("required but the layout did not place it", forgotten, StringComparison.Ordinal);
    }

    /// <summary>
    /// A member left out of the JSON keeps the default the type declares. The source-generated reader
    /// skips initializers on <c>init</c>-only properties, so these types use <c>set</c>; this pins it.
    /// </summary>
    [Fact]
    public void AbsentMembersKeepTheirDefaults()
    {
        PlanDescription bare = Assert.IsType<PlanDescription>(PlanDescriptionReader.Parse("{}", out _));
        Assert.Equal(PlanDescription.CurrentVersion, bare.Version);
        Assert.NotNull(bare.Slots);
        Assert.NotNull(bare.Path);
        Assert.NotNull(bare.Links);
        Assert.NotNull(bare.RequireTag);
        Assert.NotNull(bare.RequireSocket);
        Assert.NotNull(bare.Structures);

        RuleParseResult rules = RuleSetReader.Parse("{}");
        RuleSet set = Assert.IsType<RuleSet>(rules.Set);
        Assert.Empty(rules.Diagnostics);
        Assert.Empty(set.Rules);
    }

    /// <summary>A host function that yields nothing fails the attempt with the layout's name.</summary>
    [Fact]
    public void ANullDescriptionFailsTheAttempt()
    {
        var planner = new DescribedLayoutPlanner("quiet", _ => null);
        MacroPlan? plan = planner.Plan(Request(PolicyFixture.Recipe()), out string failure);

        Assert.Null(plan);
        Assert.Contains("'quiet' produced no plan", failure, StringComparison.Ordinal);
    }

    /// <summary>Several rule documents merge in order, budgets multiply, and an unusable one is left out with its reason.</summary>
    [Fact]
    public void RuleDocumentsMerge()
    {
        string[] documents =
        [
            TestJson.Quote("{ 'version': 1, 'rules': [ { 'target': 'cell', 'match': { 'id': 'a' }, 'allow': false } ], 'budgets': { 'encounter': 0.5 } }"),
            "{ broken",
            TestJson.Quote("{ 'version': 1, 'rules': [ { 'target': 'loot', 'match': { 'id': 'b' }, 'weight': 0 } ], 'budgets': { 'encounter': 0.5, 'loot': 2 } }"),
        ];

        RuleParseResult result = RuleSetReader.ParseMany(documents);

        RuleSet set = Assert.IsType<RuleSet>(result.Set);
        Assert.Equal(["cell", "loot"], set.Rules.Select(static r => r.Target));
        Assert.Equal(0.25f, set.Budgets?.Encounter);
        Assert.Equal(2f, set.Budgets?.Loot);
        GenDiagnostic error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("documents[1].", error.Path, StringComparison.Ordinal);
        Assert.Null(RuleSetReader.ParseMany(["{ broken"]).Set);
        Assert.Null(RuleSetReader.ParseMany([]).Set);
    }

    /// <summary>The generation request is pinned: this is the contract a script is written against.</summary>
    [Fact]
    public void TheGenerationRequestHasAPinnedShape()
    {
        var facts = new Dictionary<string, object>(StringComparer.Ordinal) { ["preset"] = "hard", ["lives"] = 3, ["luck"] = 0.5, ["ironman"] = true };
        GenContext context = new() { Seed = ulong.MaxValue, Depth = 2, UnlockFlags = new HashSet<string>(StringComparer.Ordinal) { "z", "a" }, Facts = facts };

        string json = Requests.Generation(PolicyFixture.Recipe(), context, 7UL);

        Assert.Equal(
            TestJson.Quote("{'level':{'id':'policy_test','display_name':''},'depth':2,'seed':'18446744073709551615','rng_seed':'7','unlocks':['a','z'],'facts':{'ironman':true,'lives':3,'luck':0.5,'preset':'hard'}}"),
            json);
    }

    /// <summary>The layout request carries what a layout script needs: the grid, layers, tags, structures and settings.</summary>
    [Fact]
    public void TheLayoutRequestCarriesTheRecipe()
    {
        LevelData recipe = RecipeWithShop(StructureRequirement.Optional);

        JsonNode request = Assert.IsType<JsonObject>(JsonNode.Parse(Requests.Layout(Request(recipe), 9UL)));

        Assert.Equal(8, (int?)request["grid"]?["x"]);
        Assert.Equal(8, (int?)request["grid"]?["z"]);
        Assert.Equal(0, (int?)request["layers"]?[0]);
        Assert.Equal("start", (string?)request["start_tag"]);
        Assert.Equal("shop", (string?)request["structures"]?[0]?["id"]);
        Assert.Equal(1, (int?)request["structures"]?[0]?["index"]);
        Assert.Equal("OPTIONAL", (string?)request["structures"]?[0]?["requirement"]);
        Assert.Equal(["exit", "start"], ((JsonArray)request["cell_tags"]!).Select(static tag => (string?)tag));
        Assert.Equal("9", (string?)request["rng_seed"]);
    }

    /// <summary>The plan summary lists what a script may want to refuse a level for.</summary>
    [Fact]
    public void ThePlanSummaryListsTheLevel()
    {
        LevelPlan plan = PolicyFixture.GeneratePlan(3);

        JsonNode summary = Assert.IsType<JsonObject>(JsonNode.Parse(Requests.Plan(plan)));

        Assert.Equal(plan.Fingerprint(), (string?)summary["fingerprint"]);
        Assert.Equal(plan.Cells.Count, ((JsonArray)summary["cells"]!).Count);
        Assert.Equal(plan.CriticalPath.Count, ((JsonArray)summary["path"]!).Count);
        Assert.Equal(plan.Zones.Count, ((JsonArray)summary["zones"]!).Count);
        Assert.Equal(plan.Items.Count, ((JsonArray)summary["items"]!).Count);
        Assert.Equal(plan.Props.Count, ((JsonArray)summary["props"]!).Count);
        Assert.Equal((double)plan.EncounterBudget, (double?)summary["budgets"]?["encounter"] ?? -1, 3);
        Assert.Equal(plan.Cells[0].Info.ScenePath, (string?)summary["cells"]?[0]?["scene"]);
        Assert.Equal(plan.Cells[0].Anchor.X, (int?)summary["cells"]?[0]?["anchor"]?[0]);
    }

    private static string Fingerprint(LevelData recipe, GenContext context) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(recipe, context).Plan).Fingerprint();

    private static LevelData RecipeWithShop(StructureRequirement requirement)
    {
        var door = new SocketData { Id = "path" };
        var shopCell = new CellData
        {
            ScenePath = "shop.tscn",
            Sockets = [new SocketEntryData { Slot = Int3.Zero, Face = CellFace.North, Socket = door }],
            AllowedRotations = 1,
        };
        var shop = new StructureData
        {
            Id = "shop",
            Requirement = requirement,
            AllowRotation = false,
            Pieces = [new StructurePieceData { Cell = shopCell }],
        };
        LevelData basis = PolicyFixture.Recipe();
        return new LevelData { Id = basis.Id, LayoutId = basis.LayoutId, CellSets = basis.CellSets, Structures = [shop], MaxAttempts = 2 };
    }

    private static LayoutRequest Request(LevelData recipe)
    {
        GenContext context = PolicyFixture.Context(1);
        var report = new GenReport();
        return new LayoutRequest(recipe, context, TemplateLibrary.Build(recipe, 0, report), new GenRng(1), report);
    }

    private static MacroPlan? PlanFrom(string json, LevelData recipe, out string failure) =>
        new DescribedLayoutPlanner("described", _ => TestJson.Quote(json)).Plan(Request(recipe), out failure);
}
