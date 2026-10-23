using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>A small synthetic level (start, two interchangeable middles, exit in a line) with zones, items and props on every cell, for exercising policies and rules.</summary>
internal static class PolicyFixture
{
    /// <summary>The scene of the first of two interchangeable middle cells.</summary>
    internal const string MidA = "mid_a.tscn";

    /// <summary>The scene of the second of two interchangeable middle cells.</summary>
    internal const string MidB = "mid_b.tscn";

    /// <summary>Generates the fixture level under some policies; it must succeed.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="policies">The policies to install.</param>
    /// <returns>The plan.</returns>
    internal static LevelPlan GeneratePlan(ulong seed, params IGenerationPolicy[] policies) =>
        Assert.IsType<LevelPlan>(LevelGenerator.Generate(Recipe(), Context(seed, policies)).Plan);

    /// <summary>Compares two strings ordinally.</summary>
    /// <param name="a">One string.</param>
    /// <param name="b">The other.</param>
    /// <returns>True when equal.</returns>
    internal static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>Builds a context for the fixture level with some shared policies.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="policies">The policies to install.</param>
    /// <returns>The context.</returns>
    internal static GenContext Context(ulong seed, params IGenerationPolicy[] policies)
    {
        LayoutRegistry layouts = LayoutRegistry.CreateBuiltin();
        layouts.Register(new FourInALine());
        var encounters = new EncounterCatalog(
            [new EncounterEntry("small", 1f, [], 0, -1), new EncounterEntry("big", 2f, [], 0, -1), new EncounterEntry("boss_one", 3f, ["boss"], 0, -1)],
            1f);
        return new GenContext { Seed = seed, Layouts = layouts, Encounters = encounters, Policies = policies };
    }

    /// <summary>Builds a context for the fixture level with per-generation policy sources, unlocks and facts.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="depth">The run depth.</param>
    /// <param name="sources">The policy sources.</param>
    /// <param name="unlocks">The unlock flags.</param>
    /// <param name="facts">The host facts.</param>
    /// <returns>The context.</returns>
    internal static GenContext ContextFor(
        ulong seed,
        int depth = 0,
        IReadOnlyList<IGenerationPolicySource>? sources = null,
        IReadOnlySet<string>? unlocks = null,
        IReadOnlyDictionary<string, object>? facts = null)
    {
        GenContext basis = Context(seed);
        return new GenContext
        {
            Seed = seed,
            Depth = depth,
            Layouts = basis.Layouts,
            Encounters = basis.Encounters,
            PolicySources = sources ?? [],
            UnlockFlags = unlocks ?? new HashSet<string>(StringComparer.Ordinal),
            Facts = facts ?? new Dictionary<string, object>(StringComparer.Ordinal),
        };
    }

    /// <summary>Builds a context carrying one policy.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The context.</returns>
    internal static GenContext ContextWith(IGenerationPolicy policy) => Context(1, policy);

    /// <summary>Builds the fixture recipe: a four-cell line with a zone, item spawn and prop spawn on every cell.</summary>
    /// <param name="maxAttempts">How many attempts the generator may make.</param>
    /// <param name="layoutId">The layout the recipe names.</param>
    /// <returns>The recipe.</returns>
    internal static LevelData Recipe(int maxAttempts = 4, string layoutId = FourInALine.LayoutId)
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
            LayoutId = layoutId,
            EncounterBudget = 20f,
            LootBudget = 20f,
            LootTable = table,
            PropMax = 6,
            CellSets =
            [
                new CellSetData
                {
                    Id = "set",
                    Cells = [TestCells.OneSlot("start.tscn", path, east: true, west: false, spawns: true, "start"), TestCells.OneSlot(MidA, path, east: true, west: true, spawns: true), TestCells.OneSlot(MidB, path, east: true, west: true, spawns: true), TestCells.OneSlot("end.tscn", path, east: false, west: true, spawns: true, "exit")],
                },
            ],
            MaxAttempts = maxAttempts,
        };
    }

    /// <summary>Four slots along X: start, two middles, exit.</summary>
    internal sealed class FourInALine : ILayoutPlanner
    {
        /// <summary>The id recipes name this layout by.</summary>
        public const string LayoutId = "four";

        /// <inheritdoc/>
        public string Id => LayoutId;

        /// <inheritdoc/>
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
}
