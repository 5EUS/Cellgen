# Cellgen

Deterministic, engine-agnostic procedural level generation. You hand-author
**cells** (a chunk of level that fills one or more slots of a 3D grid, with
sockets on its faces), and Cellgen turns a **recipe**, a **seed** and a **depth**
into a **plan**: which cell goes in which slot, how they connect, and what
spawns where. The same three inputs always give the same plan, on any machine.

It has no engine dependency. A host (a Godot game, a Unity game, a server)
turns its own authoring format into Cellgen's plain data
(`LevelData`, `CellData`, `SocketData`, ...), calls `LevelGenerator.Generate`,
and builds the resulting `LevelPlan` however it likes.

```csharp
var context = new GenContext { Seed = 42, Depth = 0 };
GenResult result = LevelGenerator.Generate(recipe, context);
LevelPlan? plan = result.Plan;          // null when no attempt succeeded
foreach (string line in result.Report.Lines()) { /* diagnostics */ }
string fingerprint = plan!.Fingerprint(); // same inputs, same fingerprint
```

## The pipeline

1. **Layout** — an `ILayoutPlanner` lays out the slots: which are used and why,
   which neighbours must connect, where the start and exit are. Result: a `MacroPlan`.
2. **Cells** — the solver picks a cell and a turn for every slot so that
   sockets match, tags are honoured and the exit stays reachable.
3. **Populate** — encounters, loot and props are budgeted and placed on the
   spawn points the chosen cells carry.

A failed attempt retries on a derived seed; after `MaxAttempts` the recipe's
`Fallback` is used.

## Writing a layout planner

Built-in layouts are `corridors` (a winding path with side branches) and
`wilds` (a whole-grid wilderness whose clusters come from sockets). Add your
own by implementing `ILayoutPlanner` and registering it:

```csharp
sealed class LinePlanner : ILayoutPlanner
{
    public string Id => "line";

    public MacroPlan? Plan(LayoutRequest request, out string failure)
    {
        int length = request.Rng.Range(3, 6);       // randomness only from request.Rng
        var plan = new MacroPlan();
        for (int x = 0; x < length; x++)
        {
            var slot = new Int3(x, 0, 0);
            plan.Occupy(slot, x == 0 ? SlotRole.Start : x == length - 1 ? SlotRole.Exit : SlotRole.Path);
            plan.AppendPath(slot);                  // first appended = start, last = exit
            if (x > 0) plan.Link(new Int3(x - 1, 0, 0), slot);
        }
        failure = string.Empty;
        return plan;
    }
}

LayoutRegistry layouts = LayoutRegistry.CreateBuiltin();   // a copy you may add to
layouts.Register(new LinePlanner());
var context = new GenContext { Seed = 42, Layouts = layouts };
// recipe: new LevelData { LayoutId = "line", ... }
```

`MacroPlan` is what a planner builds: `Occupy` (a slot and its `SlotRole`),
`Link` (two neighbours that must connect), `RequireTag` / `RequireSocket` (what
the cell in a slot must have), `AppendPath` (the start-to-exit path).
`StructurePlacer.Place` puts a multi-cell structure in a whole-grid layout.
Recipe-specific numbers reach a planner through `LevelData.LayoutSettings`.

**Determinism is the planner's contract.** Take every random number from
`request.Rng`; never the clock, `System.Random`, or hash-ordered iteration.
Nothing enforces this. Check a planner by generating a spread of seeds twice
and comparing `LevelPlan.Fingerprint()`, as `tests/Cellgen.Tests/CustomLayoutTests.cs` does.

The planner API is **experimental** until a planner written outside this
repository has used it.

## Policies: rules without replacing anything

Cellgen keeps the mechanism (the weighted draw, the budgets, the solver). A
host or a mod layers *policy* over it with an `IGenerationPolicy`, listed in
`GenContext.Policies`. Every member has a do-nothing default, so implement only
what you need:

| Hook | Decides |
|---|---|
| `AllowCell` / `CellWeight` | whether the solver may place a cell here, and how much more or less likely it is |
| `AllowEncounter` / `EncounterWeight` | whether a spawn zone may be given an encounter, and how likely |
| `AllowLoot` / `LootWeight` | whether an item spawn may roll an entry, and how likely |
| `AllowProp` | whether a prop spawn may place a prop |
| `AllowStructure` | whether a structure is placed in this level at all |
| `EncounterBudgetScale` / `LootBudgetScale` | a multiplier on the depth-scaled budgets |
| `RejectPlan` | a last look at the finished plan; refusing it retries on a derived seed |

```csharp
sealed class NoBossesBeforeDepth3 : IGenerationPolicy
{
    public bool AllowEncounter(in EncounterQuery q) =>
        q.Context.Depth >= 3 || !q.Encounter.Tags.Contains("boss");
}

var context = new GenContext { Seed = 42, Policies = [new NoBossesBeforeDepth3()] };
```

With several policies a veto from any one stands, weights multiply, and a
weight of zero or less removes the candidate. Policies are handed data and give
data back; they are never given a random stream. With none installed the output
is exactly what it is without the feature, and a policy that is a pure function
of its query keeps generation deterministic. Cell answers may be cached by the
solver, so they must depend on the query alone. The tests in
`tests/Cellgen.Tests/PolicyTests.cs` show each hook.

## Other host seams

- `ILootCatalog` — where a loot table's pool finds its entries.
- `EncounterCatalog` — the encounters zones can be given (ids, costs, tags, depths).
- `GenRng` — PCG32, bit-identical to Godot's `RandomNumberGenerator`
  (`tests/Cellgen.Tests/Data/gen_rng_vectors.txt` records the sequences).

## Building

```sh
dotnet build -c Release
dotnet test -c Release
sh scripts/development/check.sh      # everything CI checks
```

Targets `net8.0` and `net10.0`; warnings are errors and the strict analyzer set
in `Directory.Build.props` applies (see `AGENTS.md`).
