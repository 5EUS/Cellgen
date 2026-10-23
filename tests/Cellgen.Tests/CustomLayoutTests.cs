using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>
/// Generates whole levels with a layout planner and cells defined here, using nothing
/// but Cellgen's public API: what a host or a mod author has to work with.
/// </summary>
public sealed class CustomLayoutTests
{
    private const string LineId = "line";

    /// <summary>A registered layout drives the whole pipeline: plan, solve, populate.</summary>
    [Fact]
    public void ACustomLayoutGeneratesALevel()
    {
        GenResult result = LevelGenerator.Generate(Recipe(), Context(seed: 7));

        LevelPlan plan = Assert.IsType<LevelPlan>(result.Plan);
        Assert.InRange(plan.Cells.Count, 3, 6);
        Assert.Equal("start.tscn", plan.Cells[plan.StartCell].Info.ScenePath);
        Assert.Equal("end.tscn", plan.Cells[plan.ExitCell].Info.ScenePath);
        Assert.Equal(plan.Cells.Count, plan.CriticalPath.Count);
    }

    /// <summary>The same recipe, seed and depth give the same level; the planner drew from the request's stream.</summary>
    [Fact]
    public void ThePlanIsDeterministic()
    {
        string first = GeneratePlan(seed: 11).Fingerprint();
        string again = GeneratePlan(seed: 11).Fingerprint();

        Assert.Equal(first, again);
    }

    /// <summary>Different seeds roll different lengths: the planner really is using the random stream.</summary>
    [Fact]
    public void SeedsChangeTheLayout()
    {
        var lengths = new HashSet<int>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            lengths.Add(GeneratePlan(seed).Cells.Count);
        }

        Assert.True(lengths.Count > 1, "every seed produced the same length");
    }

    /// <summary>A batch sweep generates with the template's registry, not the default one.</summary>
    [Fact]
    public void ASweepUsesTheTemplatesLayouts()
    {
        LevelStats stats = LevelStats.Sweep(Recipe(), Context(seed: 0), count: 10);

        Assert.Empty(stats.Failures);
        Assert.Equal(0, stats.Failed);
    }

    /// <summary>A recipe naming a layout nobody registered fails loudly, not silently.</summary>
    [Fact]
    public void AnUnknownLayoutIsAnError()
    {
        LevelData recipe = Recipe(layoutId: "nonsense");

        GenResult result = LevelGenerator.Generate(recipe, new GenContext { Seed = 1 });

        Assert.Null(result.Plan);
        Assert.Contains(result.Report.Diagnostics, static d => d.Severity == GenSeverity.Error && d.Message.Contains("nonsense", StringComparison.Ordinal));
    }

    /// <summary>The shared built-in registry cannot be modified; a copy can.</summary>
    [Fact]
    public void TheSharedRegistryIsReadOnly()
    {
        Assert.Throws<InvalidOperationException>(() => LayoutRegistry.Builtin.Register(new LinePlanner()));

        LayoutRegistry mine = LayoutRegistry.CreateBuiltin();
        mine.Register(new LinePlanner());
        Assert.Equal([BuiltinLayouts.Corridors, LineId, BuiltinLayouts.Wilds], mine.Ids);
        Assert.Throws<InvalidOperationException>(() => mine.Register(new LinePlanner()));
    }

    private static GenContext Context(ulong seed)
    {
        LayoutRegistry layouts = LayoutRegistry.CreateBuiltin();
        layouts.Register(new LinePlanner());
        return new GenContext { Seed = seed, Layouts = layouts };
    }

    private static LevelPlan GeneratePlan(ulong seed) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(Recipe(), Context(seed)).Plan);

    private static LevelData Recipe(string layoutId = LineId)
    {
        var path = new SocketData { Id = "path" };
        return new LevelData
        {
            Id = "line_test",
            LayoutId = layoutId,
            LayoutSettings = new LayoutSettings(new Dictionary<string, object>(StringComparer.Ordinal) { ["min_length"] = 3, ["max_length"] = 6 }),
            CellSets = [new CellSetData { Id = "set", Cells = [Cell("start.tscn", path, east: true, west: false, "start"), Cell("mid.tscn", path, east: true, west: true), Cell("end.tscn", path, east: false, west: true, "exit")] }],
            MaxAttempts = 4,
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

        return new CellData { ScenePath = scene, Tags = tags, Sockets = sockets, AllowedRotations = 1 };
    }

    /// <summary>A straight line of slots along X, of a length rolled from the recipe's settings.</summary>
    private sealed class LinePlanner : ILayoutPlanner
    {
        public string Id => LineId;

        public MacroPlan? Plan(LayoutRequest request, out string failure)
        {
            int min = request.Def.LayoutSettings.GetInt("min_length", 3);
            int max = request.Def.LayoutSettings.GetInt("max_length", 6);
            int length = request.Rng.Range(min, max);

            var plan = new MacroPlan();
            for (int x = 0; x < length; x++)
            {
                var slot = new Int3(x, 0, 0);
                plan.Occupy(slot, RoleAt(x, length));
                plan.AppendPath(slot);
                if (x > 0)
                {
                    plan.Link(new Int3(x - 1, 0, 0), slot);
                }
            }

            failure = string.Empty;
            return plan;
        }

        private static SlotRole RoleAt(int x, int length)
        {
            if (x == 0)
            {
                return SlotRole.Start;
            }

            return (x == length - 1) ? SlotRole.Exit : SlotRole.Path;
        }
    }
}
