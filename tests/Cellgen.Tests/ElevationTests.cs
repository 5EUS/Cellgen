using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>Hilly ground: corner heights on cells, the field the layout rolls, and the solver meeting the two.</summary>
public sealed class ElevationTests
{
    private const string BlockId = "block";
    private const int Width = 5;
    private const int Depth = 4;

    /// <summary>One quarter turn moves each corner the way <see cref="SlotMath.RotateSlot"/> moves a slot; four turns come home.</summary>
    [Fact]
    public void CornersRotateLikeSlots()
    {
        int[] one = SlotMath.RotateCorners([10, 20, 30, 40], Int3.One, 1);
        Assert.Equal([20, 40, 10, 30], one);
        Assert.Equal([10, 20, 30, 40], SlotMath.RotateCorners([10, 20, 30, 40], Int3.One, 4));

        var wide = new Int3(2, 1, 1);
        Assert.Equal([6, 5, 4, 3, 2, 1], SlotMath.RotateCorners([1, 2, 3, 4, 5, 6], wide, 2));
        int[] turned = SlotMath.RotateCorners([1, 2, 3, 4, 5, 6], wide, 1);
        Assert.Equal([3, 6, 2, 5, 1, 4], turned);
    }

    /// <summary>Every cell's corners, lifted, land on the same height wherever two cells share one; neighbouring corners differ by at most a step.</summary>
    [Fact]
    public void NeighbouringCellsAgreeOnTheirSharedCorners()
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            LevelPlan plan = Generate(seed, step: 4f);
            var heights = new Dictionary<(int X, int Z), int>();
            foreach (PlannedCell cell in plan.Cells)
            {
                int cols = cell.Footprint.X + 1;
                IReadOnlyList<int> corners = cell.AbsoluteCorners();
                for (int i = 0; i < corners.Count; i++)
                {
                    (int X, int Z) at = (cell.Anchor.X + (i % cols), cell.Anchor.Z + (i / cols));
                    Assert.True(heights.TryAdd(at, corners[i]) || heights[at] == corners[i], $"seed {seed}: corner {at} is {heights[at]} and {corners[i]}");
                }
            }

            foreach (((int x, int z), int h) in heights)
            {
                foreach ((int nx, int nz) in new[] { (x + 1, z), (x, z + 1) })
                {
                    if (heights.TryGetValue((nx, nz), out int other))
                    {
                        Assert.True(Math.Abs(h - other) <= 1, $"seed {seed}: ({x},{z}) and ({nx},{nz}) differ by {Math.Abs(h - other)}");
                    }
                }
            }
        }
    }

    /// <summary>The ground really is hilly: some seed lifts a cell, and some seed places a tilted one.</summary>
    [Fact]
    public void TheGroundHasRelief()
    {
        bool lifted = false;
        bool tilted = false;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            LevelPlan plan = Generate(seed, step: 4f);
            Assert.Equal(4f, plan.ElevationStep);
            lifted |= plan.Cells.Any(static c => c.Elevation != 0);
            tilted |= plan.Cells.Any(static c => c.Info.CornerHeights.Any(static h => h != 0));
        }

        Assert.True(lifted, "no cell was ever lifted");
        Assert.True(tilted, "no tilted cell was ever placed");
    }

    /// <summary>The start and exit stand on level pads, so ordinary flat cells serve them.</summary>
    [Fact]
    public void StartAndExitAreLevel()
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            LevelPlan plan = Generate(seed, step: 4f);
            Assert.All(plan.Cells[plan.StartCell].AbsoluteCorners(), h => Assert.Equal(plan.Cells[plan.StartCell].AbsoluteCorners()[0], h));
            Assert.All(plan.Cells[plan.ExitCell].AbsoluteCorners(), h => Assert.Equal(plan.Cells[plan.ExitCell].AbsoluteCorners()[0], h));
        }
    }

    /// <summary>A slot reserved for a special cell (a required tag that is not the ground tag) stands on a level pad.</summary>
    [Fact]
    public void ReservedSlotsAreLevel()
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            LevelPlan plan = Generate(seed, step: 4f);
            PlannedCell rock = plan.Cells.Single(static c => c.Anchor == new Int3(2, 0, 2));
            Assert.Equal("rock.tscn", rock.Info.ScenePath);
            Assert.All(rock.AbsoluteCorners(), h => Assert.Equal(rock.AbsoluteCorners()[0], h));
        }
    }

    /// <summary>A corner list of the wrong length is read as flat, and the library says so rather than going quiet.</summary>
    [Fact]
    public void AWrongLengthCornerListWarns()
    {
        var report = new GenReport();
        var open = new SocketData { Id = "open" };

        CellTemplate? template = TemplateLibrary.Template(Tile("short.tscn", open, [0, 1, 2]), 1f, "cell_sets[0].cells[0]", report);

        Assert.NotNull(template);
        Assert.Contains(report.Diagnostics, static d => d.Severity == GenSeverity.Warning && d.Message.Contains("3 corner heights", StringComparison.Ordinal));
        Assert.All(template.Variants, static v => Assert.All(v.Corners, h => Assert.Equal(0, h)));
        Assert.DoesNotContain(
            Diagnostics(Tile("fine.tscn", open, [0, 1, 1, 2])),
            static d => d.Message.Contains("corner heights", StringComparison.Ordinal));
    }

    /// <summary>The same recipe, seed and depth give the same hills.</summary>
    [Fact]
    public void HillsAreDeterministic() =>
        Assert.Equal(Generate(9, step: 4f).Fingerprint(), Generate(9, step: 4f).Fingerprint());

    /// <summary>Different seeds give different ground.</summary>
    [Fact]
    public void SeedsChangeTheHills()
    {
        var prints = new HashSet<string>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= 10; seed++)
        {
            prints.Add(Generate(seed, step: 4f).Fingerprint());
        }

        Assert.True(prints.Count > 5);
    }

    /// <summary>A zero step leaves the ground level: no lifts, and no tilted cells (they cannot fit level ground).</summary>
    [Fact]
    public void ZeroStepIsLevelGround()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            LevelPlan plan = Generate(seed, step: 0f);
            Assert.Equal(0f, plan.ElevationStep);
            Assert.All(plan.Cells, static c => Assert.Equal(0, c.Elevation));
            Assert.All(plan.Cells, static c => Assert.All(c.Info.CornerHeights, h => Assert.Equal(0, h)));
        }
    }

    /// <summary>Flat cells alone cannot climb: with no tilted cells in the catalog, whatever generates uses only flat ones.</summary>
    [Fact]
    public void AFlatCatalogCannotClimb()
    {
        LevelData recipe = Recipe(step: 4f, tilted: false);

        GenResult result = LevelGenerator.Generate(recipe, Context(3));

        Assert.True(result.Plan is null || result.Plan.Cells.All(static c => c.Info.CornerHeights.All(static h => h == 0)));
    }

    private static IReadOnlyList<GenDiagnostic> Diagnostics(CellData cell)
    {
        var report = new GenReport();
        TemplateLibrary.Template(cell, 1f, "cell", report);
        return report.Diagnostics;
    }

    private static GenContext Context(ulong seed)
    {
        LayoutRegistry layouts = LayoutRegistry.CreateBuiltin();
        layouts.Register(new ElevationLayout(BlockId, new BlockPlanner()));
        return new GenContext { Seed = seed, Layouts = layouts };
    }

    private static LevelPlan Generate(ulong seed, float step) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(Recipe(step, tilted: true), Context(seed)).Plan);

    private static LevelData Recipe(float step, bool tilted)
    {
        var open = new SocketData { Id = "open", IsBoundaryOk = true };
        var cells = new List<CellData>
        {
            Tile("start.tscn", open, [0, 0, 0, 0], "start"),
            Tile("end.tscn", open, [0, 0, 0, 0], "exit"),
            Tile("rock.tscn", open, [0, 0, 0, 0], "rock"),
        };

        // Every tilt a corner can have against its neighbours: corners 0-2, the lowest at 0.
        for (int a = 0; a < 3; a++)
        {
            for (int b = 0; b < 3; b++)
            {
                for (int c = 0; c < 3; c++)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        bool flat = a == 0 && b == 0 && c == 0 && d == 0;
                        if ((flat || tilted) && Math.Min(Math.Min(a, b), Math.Min(c, d)) == 0)
                        {
                            cells.Add(Tile($"tile_{a}{b}{c}{d}.tscn", open, [a, b, c, d]));
                        }
                    }
                }
            }
        }

        return new LevelData
        {
            Id = "hills_test",
            LayoutId = BlockId,
            LayoutSettings = new LayoutSettings(new Dictionary<string, object>(StringComparer.Ordinal) { ["elevation_step"] = step, ["elevation_relief"] = 5, ["elevation_scale"] = 5 }),
            CellSets = [new CellSetData { Id = "set", Cells = cells }],
            MaxAttempts = 8,
        };
    }

    private static CellData Tile(string scene, SocketData socket, int[] corners, params string[] tags) => new()
    {
        ScenePath = scene,
        Tags = tags,
        CornerHeights = corners,
        AllowedRotations = 1,
        Sockets = [.. new[] { CellFace.North, CellFace.East, CellFace.South, CellFace.West }
            .Select(face => new SocketEntryData { Slot = Int3.Zero, Face = face, Socket = socket })],
    };

    /// <summary>A block of slots with a snaking path from one corner to the other.</summary>
    private sealed class BlockPlanner : ILayoutPlanner
    {
        public string Id => "block_inner";

        public MacroPlan? Plan(LayoutRequest request, out string failure)
        {
            var plan = new MacroPlan();
            var path = new List<Int3>();
            for (int x = 0; x < Width; x++)
            {
                path.Add(new Int3(x, 0, 0));
            }

            for (int z = 1; z < Depth; z++)
            {
                path.Add(new Int3(Width - 1, 0, z));
            }

            for (int i = 0; i < path.Count; i++)
            {
                SlotRole role = SlotRole.Path;
                if (i == 0)
                {
                    role = SlotRole.Start;
                }
                else if (i == path.Count - 1)
                {
                    role = SlotRole.Exit;
                }

                plan.Occupy(path[i], role);
                plan.AppendPath(path[i]);
                if (i > 0)
                {
                    plan.Link(path[i - 1], path[i]);
                }
            }

            for (int z = 1; z < Depth; z++)
            {
                for (int x = 0; x < Width - 1; x++)
                {
                    plan.Occupy(new Int3(x, 0, z), SlotRole.Branch);
                }
            }

            plan.RequireTag(new Int3(2, 0, 2), "rock");

            failure = string.Empty;
            return plan;
        }
    }
}
