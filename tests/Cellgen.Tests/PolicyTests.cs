using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>Every <see cref="IGenerationPolicy"/> hook, against a small level made of synthetic cells.</summary>
public sealed class PolicyTests
{
    private const string MidA = "mid_a.tscn";
    private const string MidB = "mid_b.tscn";

    /// <summary>A policy that overrides nothing changes nothing: the fingerprint is the same.</summary>
    [Fact]
    public void ADefaultPolicyIsInert()
    {
        for (ulong seed = 1; seed <= 15; seed++)
        {
            Assert.Equal(GeneratePlan(seed).Fingerprint(), GeneratePlan(seed, new InertPolicy()).Fingerprint());
        }
    }

    /// <summary>A vetoed cell is never placed, but the level still generates from the others.</summary>
    [Fact]
    public void AllowCellVetoesACell()
    {
        for (ulong seed = 1; seed <= 15; seed++)
        {
            LevelPlan plan = GeneratePlan(seed, new VetoCell(MidB));
            Assert.DoesNotContain(plan.Cells, static c => Same(c.Info.ScenePath, MidB));
            Assert.Contains(plan.Cells, static c => Same(c.Info.ScenePath, MidA));
        }
    }

    /// <summary>A weight of zero removes a cell like a veto; a positive weight only reorders.</summary>
    [Fact]
    public void CellWeightZeroRemovesACell()
    {
        for (ulong seed = 1; seed <= 15; seed++)
        {
            Assert.DoesNotContain(GeneratePlan(seed, new WeighCell(MidA, 0f)).Cells, static c => Same(c.Info.ScenePath, MidA));
        }

        var heavy = new WeighCell(MidB, 1000f);
        int middles = 0;
        int ofB = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            foreach (PlannedCell cell in GeneratePlan(seed, heavy).Cells.Where(static c => Same(c.Info.ScenePath, MidA) || Same(c.Info.ScenePath, MidB)))
            {
                middles++;
                ofB += Same(cell.Info.ScenePath, MidB) ? 1 : 0;
            }
        }

        Assert.True(ofB > middles * 0.9, $"{ofB} of {middles} middles were B");
    }

    /// <summary>A vetoed encounter is never given to a zone; the others still are.</summary>
    [Fact]
    public void AllowEncounterVetoesAnEncounter()
    {
        IEnumerable<string> ids = Enumerable.Range(1, 15).SelectMany(seed => GeneratePlan((ulong)seed, new VetoEncounter("big")).Zones.Select(static z => z.EncounterId));

        Assert.DoesNotContain("big", ids);
        Assert.Contains("small", ids);
    }

    /// <summary>The budget scales multiply into the depth-scaled budgets.</summary>
    [Fact]
    public void BudgetScalesApply()
    {
        LevelPlan plain = GeneratePlan(3);
        LevelPlan scaled = GeneratePlan(3, new ScaleBudgets(0.5f, 2f));

        Assert.Equal(plain.EncounterBudget * 0.5f, scaled.EncounterBudget);
        Assert.Equal(plain.LootBudget * 2f, scaled.LootBudget);
    }

    /// <summary>A vetoed loot entry is never rolled.</summary>
    [Fact]
    public void AllowLootVetoesAnEntry()
    {
        IEnumerable<string> ids = Enumerable.Range(1, 15).SelectMany(seed => GeneratePlan((ulong)seed, new VetoLoot("item_b")).Items.Select(static i => i.ItemId));

        Assert.DoesNotContain("item_b", ids);
        Assert.Contains("item_a", ids);
    }

    /// <summary>A vetoed prop is never placed.</summary>
    [Fact]
    public void AllowPropVetoesAProp()
    {
        IEnumerable<string> ids = Enumerable.Range(1, 15).SelectMany(seed => GeneratePlan((ulong)seed, new VetoProp("prop_b")).Props.Select(static p => p.PropId));

        Assert.DoesNotContain("prop_b", ids);
        Assert.Contains("prop_a", ids);
    }

    /// <summary>A vetoed structure rolls a count of zero; an allowed one rolls its required count.</summary>
    [Fact]
    public void AllowStructureVetoesAStructure()
    {
        var shop = new StructureData { Id = "shop", Requirement = StructureRequirement.Required, MinCount = 1, MaxCount = 1 };
        LevelData recipe = Recipe();
        GenContext open = Context(1);
        GenContext shut = Context(1, new VetoStructure("shop"));

        Assert.Equal(1, StructurePlacer.CountFor(recipe, shop, new GenRng(1), open));
        Assert.Equal(0, StructurePlacer.CountFor(recipe, shop, new GenRng(1), shut));
    }

    /// <summary>A refused plan fails the attempt; with every plan refused, generation ends with an error that says why.</summary>
    [Fact]
    public void RejectPlanFailsTheAttempt()
    {
        GenResult result = LevelGenerator.Generate(Recipe(), Context(1, new RejectAll()));

        Assert.Null(result.Plan);
        Assert.Contains(result.Report.Lines(), static l => l.Contains("never good enough", StringComparison.Ordinal));
    }

    /// <summary>A policy that refuses only some plans makes the generator retry until one passes.</summary>
    [Fact]
    public void RejectPlanRetriesOnADerivedSeed()
    {
        var retried = false;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            GenResult result = LevelGenerator.Generate(Recipe(maxAttempts: 30), Context(seed, new RejectMidA()));
            LevelPlan plan = Assert.IsType<LevelPlan>(result.Plan);
            Assert.Equal(MidB, plan.Cells[1].Info.ScenePath);
            retried |= result.Report.Attempts > 1;
        }

        Assert.True(retried, "no seed ever needed a second attempt");
    }

    /// <summary>With two policies a veto from either stands and weights multiply.</summary>
    [Fact]
    public void PoliciesCompose()
    {
        IEnumerable<string> ids = Enumerable.Range(1, 15).SelectMany(seed => GeneratePlan((ulong)seed, new VetoEncounter("big"), new ScaleBudgets(1f, 1f)).Zones.Select(static z => z.EncounterId));

        Assert.DoesNotContain("big", ids);
    }

    private static LevelPlan GeneratePlan(ulong seed, params IGenerationPolicy[] policies) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(Recipe(), Context(seed, policies)).Plan);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    private static GenContext Context(ulong seed, params IGenerationPolicy[] policies)
    {
        LayoutRegistry layouts = LayoutRegistry.CreateBuiltin();
        layouts.Register(new FourInALine());
        var encounters = new EncounterCatalog(
            [new EncounterEntry("small", 1f, [], 0, -1), new EncounterEntry("big", 2f, [], 0, -1)],
            1f);
        return new GenContext { Seed = seed, Layouts = layouts, Encounters = encounters, Policies = policies };
    }

    private static LevelData Recipe(int maxAttempts = 4)
    {
        var path = new SocketData { Id = "path" };
        var table = new LootTableData
        {
            Id = "table",
            Entries = [new LootEntryData { ItemId = "item_a" }, new LootEntryData { ItemId = "item_b" }],
        };
        return new LevelData
        {
            Id = "policy_test",
            LayoutId = FourInALine.LayoutId,
            EncounterBudget = 20f,
            LootBudget = 20f,
            LootTable = table,
            PropMax = 6,
            CellSets =
            [
                new CellSetData
                {
                    Id = "set",
                    Cells = [Cell("start.tscn", path, east: true, west: false, "start"), Cell(MidA, path, east: true, west: true), Cell(MidB, path, east: true, west: true), Cell("end.tscn", path, east: false, west: true, "exit")],
                },
            ],
            MaxAttempts = maxAttempts,
        };
    }

    private static CellData Cell(string scene, SocketData socket, bool east, bool west, params string[] tags)
    {
        var sockets = new List<SocketEntryData>();
        if (east)
        {
            sockets.Add(new SocketEntryData { Slot = Int3.Zero, Face = CellFace.East, Socket = socket });
        }

        if (west)
        {
            sockets.Add(new SocketEntryData { Slot = Int3.Zero, Face = CellFace.West, Socket = socket });
        }

        return new CellData
        {
            ScenePath = scene,
            Tags = tags,
            Sockets = sockets,
            AllowedRotations = 1,
            Zones = [new ZoneData { NodePath = "Zone", SpawnPoints = [new Vec3(0f, 0f, 0f)] }],
            Items = [new ItemSpawnData { NodePath = "Item" }],
            PropSpawns = [new PropSpawnData { NodePath = "Prop", PropIds = ["prop_a", "prop_b"], Chance = 1f }],
        };
    }

    /// <summary>Four slots along X: start, two middles, exit.</summary>
    private sealed class FourInALine : ILayoutPlanner
    {
        public const string LayoutId = "four";

        public string Id => LayoutId;

        public MacroPlan? Plan(LayoutRequest request, out string failure)
        {
            var plan = new MacroPlan();
            for (int x = 0; x < 4; x++)
            {
                var slot = new Int3(x, 0, 0);
                plan.Occupy(slot, RoleAt(x));
                plan.AppendPath(slot);
                if (x > 0)
                {
                    plan.Link(new Int3(x - 1, 0, 0), slot);
                }
            }

            failure = string.Empty;
            return plan;
        }

        private static SlotRole RoleAt(int x)
        {
            if (x == 0)
            {
                return SlotRole.Start;
            }

            return (x == 3) ? SlotRole.Exit : SlotRole.Path;
        }
    }

    private sealed class InertPolicy : IGenerationPolicy
    {
    }

    private sealed class VetoCell(string scene) : IGenerationPolicy
    {
        public bool AllowCell(in CellQuery query) => !Same(query.Cell.ScenePath, scene);
    }

    private sealed class WeighCell(string scene, float weight) : IGenerationPolicy
    {
        public float CellWeight(in CellQuery query) => Same(query.Cell.ScenePath, scene) ? weight : 1f;
    }

    private sealed class VetoEncounter(string id) : IGenerationPolicy
    {
        public bool AllowEncounter(in EncounterQuery query) => !Same(query.Encounter.Id, id);
    }

    private sealed class ScaleBudgets(float encounters, float loot) : IGenerationPolicy
    {
        public float EncounterBudgetScale(LevelData def, GenContext context) => encounters;

        public float LootBudgetScale(LevelData def, GenContext context) => loot;
    }

    private sealed class VetoLoot(string id) : IGenerationPolicy
    {
        public bool AllowLoot(in LootQuery query) => !Same(query.Entry.ItemId, id);
    }

    private sealed class VetoProp(string id) : IGenerationPolicy
    {
        public bool AllowProp(in PropQuery query) => !Same(query.PropId, id);
    }

    private sealed class VetoStructure(string id) : IGenerationPolicy
    {
        public bool AllowStructure(in StructureQuery query) => !Same(query.Structure.Id, id);
    }

    private sealed class RejectAll : IGenerationPolicy
    {
        public string? RejectPlan(LevelPlan plan) => "never good enough";
    }

    private sealed class RejectMidA : IGenerationPolicy
    {
        public string? RejectPlan(LevelPlan plan) => Same(plan.Cells[1].Info.ScenePath, MidA) ? "starts with A" : null;
    }
}
