namespace Cellgen;

/// <summary>A recipe for one kind of level. A recipe, a seed and a run depth always give the same plan.</summary>
public sealed class LevelData
{
    /// <summary>Gets the stable identity, for saves, diagnostics and the preview.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the player-facing name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the cell pools the level draws from.</summary>
    public IReadOnlyList<CellSetData> CellSets { get; init; } = [];

    /// <summary>Gets the run depths this recipe may be chosen for; MaxDepth -1 for no limit.</summary>
    public int MinDepth { get; init; }

    /// <summary>Gets the last run depth this recipe may be chosen for; -1 for no limit.</summary>
    public int MaxDepth { get; init; } = -1;

    /// <summary>Gets how likely the recipe is relative to others eligible at the same depth.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Gets the horizontal grid bounds, in slots. The level never extends past them.</summary>
    public Int2 GridSize { get; init; } = new(8, 8);

    /// <summary>Gets the id of the layout planner that lays the slots out; see <see cref="Generation.BuiltinLayouts"/>, or a planner registered in <see cref="Generation.GenContext.Layouts"/>.</summary>
    public string LayoutId { get; init; } = Generation.BuiltinLayouts.Corridors;

    /// <summary>Gets the settings the chosen layout planner documents; <see cref="LayoutSettings.Empty"/> for none.</summary>
    public LayoutSettings LayoutSettings { get; init; } = LayoutSettings.Empty;

    /// <summary>Gets the layers the level may use. <c>[0]</c> is a surface-only level; <c>[0, -1]</c> adds one below.</summary>
    public IReadOnlyList<int> AllowedLayers { get; init; } = [0];

    /// <summary>Gets the shortest start-to-exit path, in slots.</summary>
    public int CriticalPathMin { get; init; } = 5;

    /// <summary>Gets the longest start-to-exit path, in slots.</summary>
    public int CriticalPathMax { get; init; } = 8;

    /// <summary>Gets how many side branches grow off the level's paths.</summary>
    public int BranchCount { get; init; } = 3;

    /// <summary>Gets the shortest branch, in slots.</summary>
    public int BranchLengthMin { get; init; } = 1;

    /// <summary>Gets the longest branch, in slots.</summary>
    public int BranchLengthMax { get; init; } = 3;

    /// <summary>Gets the probability a step of the path changes layer, where the cells allow it.</summary>
    public float LayerChangeChance { get; init; } = 0.25f;

    /// <summary>Gets the tag a cell needs to be the start.</summary>
    public string StartTag { get; init; } = "start";

    /// <summary>Gets the tag a cell needs to be the exit.</summary>
    public string ExitTag { get; init; } = "exit";

    /// <summary>Gets how many tall landmark slots (two layers high) a the wilds layout level reserves. Needs layer 1 in <see cref="AllowedLayers"/>.</summary>
    public int LandmarkCount { get; init; } = 4;

    /// <summary>Gets the tag a cell needs to fill a landmark slot.</summary>
    public string LandmarkTag { get; init; } = "wild_tall";

    /// <summary>Gets the tag the border ring's cells need.</summary>
    public string BorderTag { get; init; } = "wilds_border";

    /// <summary>Gets the tag every other cell needs.</summary>
    public string GroundTag { get; init; } = "wild";

    /// <summary>Gets the least Manhattan distance, in slots, between the start and the exit.</summary>
    public int MinStartExitDistance { get; init; } = 16;

    /// <summary>Gets the structures the level may or must contain.</summary>
    public IReadOnlyList<StructureData> Structures { get; init; } = [];

    /// <summary>Gets the encounter budget at depth 0. It grows by the content's <c>budget_growth</c> per depth.</summary>
    public float EncounterBudget { get; init; } = 6f;

    /// <summary>Gets tags every encounter in the level must carry one of; empty allows any.</summary>
    public IReadOnlyList<string> EncounterTags { get; init; } = [];

    /// <summary>Gets the level's default loot table.</summary>
    public LootTableData? LootTable { get; init; }

    /// <summary>Gets the loot budget at depth 0.</summary>
    public float LootBudget { get; init; } = 4f;

    /// <summary>Gets the most props the level places, on prop spawns in cells that do not carry a tag in <see cref="PropExcludedTags"/>. Zero places none.</summary>
    public int PropMax { get; init; }

    /// <summary>Gets tags that keep props out of a cell: no man's land, a safe room.</summary>
    public IReadOnlyList<string> PropExcludedTags { get; init; } = [];

    /// <summary>Gets how many seeds the generator tries before giving up on this recipe.</summary>
    public int MaxAttempts { get; init; } = 24;

    /// <summary>Gets the recipe used when every attempt fails. Keep it simple enough that it always succeeds.</summary>
    public LevelData? Fallback { get; init; }
}
