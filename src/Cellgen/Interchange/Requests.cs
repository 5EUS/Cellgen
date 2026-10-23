using System.Globalization;
using System.Text.Json.Nodes;

using Cellgen.Generation;

namespace Cellgen.Interchange;

/// <summary>
/// Writes what a script is told, as JSON: the request for a generation, the request for one layout
/// attempt, and a finished plan. Built by hand (no reflection) so every key and its order is
/// explicit here, and so any host in any language can read them. Seeds are decimal strings,
/// because a 64-bit seed does not fit the numbers most scripting languages have.
/// </summary>
public static class Requests
{
    /// <summary>Writes the request for a generation: which level, the depth, the facts the host set.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth, unlock flags and facts.</param>
    /// <param name="rngSeed">The seed of the random stream the script draws from; take it from a stage's own stream.</param>
    /// <returns>The JSON document.</returns>
    public static string Generation(LevelData def, GenContext context, ulong rngSeed) => Header(def, context, rngSeed).ToJsonString();

    /// <summary>Writes the request for one layout attempt: everything in <see cref="Generation"/> plus the grid, layers, tags, structures and settings.</summary>
    /// <param name="request">The attempt.</param>
    /// <param name="rngSeed">The seed of the random stream the script draws from.</param>
    /// <returns>The JSON document.</returns>
    public static string Layout(LayoutRequest request, ulong rngSeed)
    {
        LevelData def = request.Def;
        JsonObject document = Header(def, request.Context, rngSeed);
        document["grid"] = new JsonObject { ["x"] = def.GridSize.X, ["z"] = def.GridSize.Y };
        document["layers"] = Numbers(def.AllowedLayers.Count > 0 ? def.AllowedLayers : [0]);
        document["start_tag"] = def.StartTag;
        document["exit_tag"] = def.ExitTag;
        document["critical_path_min"] = def.CriticalPathMin;
        document["critical_path_max"] = def.CriticalPathMax;
        document["branch_count"] = def.BranchCount;
        document["branch_length_min"] = def.BranchLengthMin;
        document["branch_length_max"] = def.BranchLengthMax;
        document["layer_change_chance"] = def.LayerChangeChance;
        document["min_start_exit_distance"] = def.MinStartExitDistance;
        document["settings"] = Entries(def.LayoutSettings.Values);
        document["structures"] = Structures(def);
        document["cell_tags"] = Strings(def.CellSets.SelectMany(static set => set.Cells).SelectMany(static cell => cell.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return document.ToJsonString();
    }

    /// <summary>Writes a finished plan, for a script that may refuse it.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The JSON document.</returns>
    public static string Plan(LevelPlan plan)
    {
        var document = new JsonObject
        {
            ["level"] = plan.Def.Id,
            ["seed"] = plan.Seed.ToString(CultureInfo.InvariantCulture),
            ["depth"] = plan.Depth,
            ["fingerprint"] = plan.Fingerprint(),
            ["start_cell"] = plan.StartCell,
            ["exit_cell"] = plan.ExitCell,
            ["cells"] = Array(plan.Cells.Select(Cell)),
            ["path"] = Array(plan.CriticalPath.Select(Slot)),
            ["zones"] = Array(plan.Zones.Select(Zone)),
            ["items"] = Array(plan.Items.Select(Item)),
            ["props"] = Array(plan.Props.Select(Prop)),
            ["empty_zones"] = plan.EmptyZones,
            ["budgets"] = new JsonObject
            {
                ["encounter"] = plan.EncounterBudget,
                ["encounter_spent"] = plan.EncounterSpent,
                ["loot"] = plan.LootBudget,
                ["loot_spent"] = plan.LootSpent,
            },
        };
        return document.ToJsonString();
    }

    private static JsonObject Header(LevelData def, GenContext context, ulong rngSeed) => new()
    {
        ["level"] = new JsonObject { ["id"] = def.Id, ["display_name"] = def.DisplayName },
        ["depth"] = context.Depth,
        ["seed"] = context.Seed.ToString(CultureInfo.InvariantCulture),
        ["rng_seed"] = rngSeed.ToString(CultureInfo.InvariantCulture),
        ["unlocks"] = Strings(context.UnlockFlags.Order(StringComparer.Ordinal)),
        ["facts"] = Entries(context.Facts),
    };

    private static JsonArray Structures(LevelData def) => Array(def.Structures.Select(static (structure, index) => new JsonObject
    {
        ["index"] = index + 1,
        ["id"] = structure.Id,
        ["requirement"] = structure.Requirement.ToString().ToUpperInvariant(),
        ["min_count"] = structure.MinCount,
        ["max_count"] = structure.MaxCount,
        ["layer"] = structure.Layer,
    }));

    private static JsonObject Cell(PlannedCell cell) => new()
    {
        ["index"] = cell.Index,
        ["scene"] = cell.Info.ScenePath,
        ["anchor"] = Slot(cell.Anchor),
        ["footprint"] = Slot(cell.Footprint),
        ["rotation"] = cell.Rotation,
        ["tags"] = Strings(cell.Info.Tags),
        ["structure"] = cell.StructureId,
    };

    private static JsonObject Zone(PlannedZone zone) => new()
    {
        ["cell"] = zone.CellIndex,
        ["node"] = zone.Zone.NodePath,
        ["encounter"] = zone.EncounterId,
        ["cost"] = zone.Cost,
    };

    private static JsonObject Item(PlannedItem item) => new()
    {
        ["cell"] = item.CellIndex,
        ["node"] = item.Spawn.NodePath,
        ["item"] = item.ItemId,
        ["price"] = item.Price,
    };

    private static JsonObject Prop(PlannedProp prop) => new()
    {
        ["cell"] = prop.CellIndex,
        ["node"] = prop.Spawn.NodePath,
        ["prop"] = prop.PropId,
    };

    private static JsonArray Slot(Int3 slot) => Numbers([slot.X, slot.Y, slot.Z]);

    private static JsonArray Numbers(IEnumerable<int> values) => Array(values.Select(static v => (JsonNode?)JsonValue.Create(v)));

    private static JsonArray Strings(IEnumerable<string> values) => Array(values.Select(static v => (JsonNode?)JsonValue.Create(v)));

    private static JsonArray Array(IEnumerable<JsonNode?> items) => new([.. items]);

    private static JsonObject Entries(IEnumerable<KeyValuePair<string, object>> entries)
    {
        var result = new JsonObject();
        foreach (KeyValuePair<string, object> entry in entries.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            result[entry.Key] = Value(entry.Value);
        }

        return result;
    }

    private static JsonValue? Value(object value) => value switch
    {
        bool flag => JsonValue.Create(flag),
        int or long or short or byte or sbyte or ushort or uint => JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        float or double or decimal => JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };
}
