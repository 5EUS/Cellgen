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

## Rules: policies as data

Most policy is "forbid these, favour those, scale the budgets", and that needs no code.
A `RuleSet` says it as data, `RulePolicy` applies it natively (a lookup per candidate), and
the JSON form is a contract anything can write: a data file, an editor, or a script in
whatever language the host embeds. Cellgen itself runs no scripts.

```json
{ "version": 1,
  "rules": [
    { "target": "encounter", "match": { "tag": "boss", "max_depth": 2 }, "allow": false },
    { "target": "cell", "match": { "tags_any": ["trench"], "level": "frontline_01" }, "weight": 2.5 },
    { "target": "loot", "match": { "id_prefix": "mymod:" }, "weight": 0 }
  ],
  "budgets": { "encounter": 0.5, "loot": 2 } }
```

Targets are `cell`, `encounter`, `loot`, `prop` and `structure`. A match is any of `id`,
`ids`, `id_prefix` (one only), `tag`, `tags_any`, `tags_all`, `min_depth`, `max_depth`,
`level` (the recipe's id) and `unlock`; every field set must hold. Every matching rule
applies: any `"allow": false` stands, weights multiply. Depth and unlock conditions are
resolved once when the generation starts. `RuleSetReader.Parse` never throws: a malformed
document or an unknown key is an error and the set is rejected; a bad rule (unknown target,
two id forms, nothing to do) is dropped with a warning and the rest apply; weights are
clamped to 0..1000 and budget scales to 0.1..10.

```csharp
var rules = RuleSetReader.Parse(json);                 // diagnostics in rules.Diagnostics
var context = new GenContext { Seed = 42, PolicySources = [new RulePolicySource(rules.Set!)] };
```

## Layouts from data

A layout does not have to be C#. `DescribedLayoutPlanner(id, describe)` takes any function that
returns a **plan description** as JSON (a script, a data file, a service) and builds the plan from
it, after checking everything natively: bounds against the recipe's grid and layers, no duplicate
slots, a connected path from a `start` to an `exit`, links between neighbours, tags and sockets on
slots that exist, structures by index with a region to place them in. A bad description fails the
attempt with every problem named by path; it never corrupts one.

```json
{ "version": 1,
  "slots": [ { "at": [0,0,0], "role": "start" }, { "at": [1,0,0], "role": "path" }, { "at": [2,0,0], "role": "exit" } ],
  "path": [ [0,0,0], [1,0,0], [2,0,0] ],
  "links": [], "require_tag": [], "require_socket": [],
  "structures": [ { "index": 1, "count": 1, "region": { "usable": { "x": [1,6], "z": [3,5] } } } ] }
```

Consecutive path slots are always linked. A structure listed here is placed by Cellgen itself
(the search is native), inside its region, with the attempt's random stream; a recipe's
`required` structure that the description does not place fails the attempt.
`Requests.Layout(request, rngSeed)` is what a layout script is told: the grid, layers, tags,
structures and settings; `Requests.Generation` is the smaller request for rules; `Requests.Plan`
summarises a finished plan for a script that may refuse it. Seeds are decimal strings.

## Policy sources and facts

`GenContext.Policies` holds policies shared by every generation. A host that needs one made
**per generation** (a compiled rule set, a script session) adds an `IGenerationPolicySource`
to `GenContext.PolicySources`: it is asked once per recipe attempted, the fallback recipe
asks again, and the `PolicyScope` it returns runs its cleanup when the generation ends.
`RuleSetReader.ParseMany` merges the rule documents several scripts return into one set.
`GenContext.Facts` carries whatever the host knows about the run (preset, campaign); policies
and sources may read it and Cellgen never does.

## Other host seams

- `ILootCatalog` — where a loot table's pool finds its entries.
- `EncounterCatalog` — the encounters zones can be given (ids, costs, tags, depths).
- `GenRng` — PCG32, bit-identical to Godot's `RandomNumberGenerator`; specified in
  [docs/rng.md](docs/rng.md) with test vectors in `spec/`, for hosts and ports in other languages.

## Building

```sh
dotnet build -c Release
dotnet test -c Release
sh scripts/development/check.sh      # everything CI checks
```

Targets `net8.0` and `net10.0`; warnings are errors and the strict analyzer set
in `Directory.Build.props` applies (see `AGENTS.md`).
