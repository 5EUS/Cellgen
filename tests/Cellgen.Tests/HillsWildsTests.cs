using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>Hills over the real wilds layout.</summary>
public sealed class HillsWildsTests
{
    /// <summary>The built-in hills layout generates over a wilds catalog of flat cells, a border and the five tilt shapes.</summary>
    [Fact]
    public void HillsGenerateOverTheWilds()
    {
        LevelData recipe = Recipe();
        int failed = 0;
        string last = string.Empty;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            GenResult result = LevelGenerator.Generate(recipe, new GenContext { Seed = seed });
            if (result.Plan is null || result.Report.Attempts > 6)
            {
                failed++;
                last = string.Join("; ", result.Report.Diagnostics.Select(static d => d.Message).Take(6));
            }
        }

        Assert.True(failed <= 2, $"{failed}/20 seeds needed many attempts or failed: {last}");
    }

    /// <summary>Most of the ground slopes: the flat cells are the pads, the plateaus and the odd level patch, not the bulk.</summary>
    [Fact]
    public void MostOfTheGroundIsNotFlat()
    {
        LevelData recipe = Recipe(new Int2(24, 20));
        int flat = 0;
        int total = 0;
        int relief = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            LevelPlan? plan = LevelGenerator.Generate(recipe, new GenContext { Seed = seed }).Plan;
            Assert.NotNull(plan);
            var heights = new List<int>();
            foreach (PlannedCell cell in plan.Cells.Where(static c => !string.Equals(c.Info.ScenePath, "border.tscn", StringComparison.Ordinal)))
            {
                IReadOnlyList<int> corners = cell.AbsoluteCorners();
                heights.AddRange(corners);
                total++;
                flat += corners.All(h => h == corners[0]) ? 1 : 0;
            }

            relief = Math.Max(relief, heights.Max() - heights.Min());
        }

        Assert.True(flat < total * 0.35, $"{flat} of {total} ground cells are flat");
        Assert.True(flat > 0, "no level flats at all: the plateaus and pads should be flat");
        Assert.True(relief >= 4, $"the tallest relief was only {relief} steps");
    }

    /// <summary>A tall landmark stands on a level pad like the start does: it is a flat cell, and the hills climb round it.</summary>
    [Fact]
    public void LandmarksStandOnLevelPads()
    {
        LevelData recipe = Recipe(new Int2(24, 20), landmarks: true);
        int towers = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            GenResult result = LevelGenerator.Generate(recipe, new GenContext { Seed = seed });
            Assert.NotNull(result.Plan);
            foreach (PlannedCell cell in result.Plan.Cells.Where(static c => c.Info.HasTag("wild_tall")))
            {
                towers++;
                Assert.Equal(new Int3(1, 2, 1), cell.Footprint);
                Assert.All(cell.AbsoluteCorners(), h => Assert.Equal(cell.AbsoluteCorners()[0], h));
            }
        }

        Assert.True(towers >= 12, $"only {towers} landmarks were placed over 12 seeds");
    }

    private static LevelData Recipe(Int2? grid = null, bool landmarks = false)
    {
        var ground = new SocketData { Id = "wild_ground", IsBoundaryOk = true };
        var cells = new List<CellData>
        {
            Tile("start.tscn", ground, [0, 0, 0, 0], "start"),
            Tile("exit.tscn", ground, [0, 0, 0, 0], "exit"),
            Tile("flat.tscn", ground, [0, 0, 0, 0], "wild"),
            new() { ScenePath = "border.tscn", Tags = ["wilds_border"] },
        };
        foreach (int[] shape in new[] { new[] { 0, 0, 0, 1 }, [0, 0, 1, 1], [0, 1, 1, 0], [0, 1, 1, 1], [0, 1, 1, 2] })
        {
            cells.Add(Tile($"tilt_{string.Concat(shape)}.tscn", ground, shape, "wild"));
        }

        if (landmarks)
        {
            cells.Add(new CellData
            {
                ScenePath = "tower.tscn",
                Tags = ["wild_tall"],
                Footprint = new Int3(1, 2, 1),
                Sockets = [.. new[] { CellFace.North, CellFace.East, CellFace.South, CellFace.West }
                    .Select(face => new SocketEntryData { Slot = Int3.Zero, Face = face, Socket = ground })],
            });
        }

        return new LevelData
        {
            Id = "hills_wilds",
            LayoutId = BuiltinLayouts.Hills,
            GridSize = grid ?? new Int2(12, 10),
            AllowedLayers = landmarks ? [0, 1] : [0],
            LandmarkCount = landmarks ? 3 : 0,
            MinStartExitDistance = 8,
            LayoutSettings = new LayoutSettings(new Dictionary<string, object>(StringComparer.Ordinal) { [ElevationLayout.StepSetting] = 4f }),
            CellSets = [new CellSetData { Id = "set", Cells = cells }],
            MaxAttempts = 24,
        };
    }

    private static CellData Tile(string scene, SocketData socket, int[] corners, string tag) => new()
    {
        ScenePath = scene,
        Tags = [tag],
        CornerHeights = corners,
        Sockets = [.. new[] { CellFace.North, CellFace.East, CellFace.South, CellFace.West }
            .Select(face => new SocketEntryData { Slot = Int3.Zero, Face = face, Socket = socket })],
    };
}
