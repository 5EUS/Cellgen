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
