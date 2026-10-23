namespace Cellgen.Interchange;

/// <summary>
/// A layout as data: the JSON a layout script (or anything else outside C#) returns, which
/// <see cref="DescribedLayoutPlanner"/> checks and turns into a plan. Coordinates are
/// <c>[x, y, z]</c> slots, <c>y</c> being the layer. Consecutive slots of <see cref="Path"/>
/// are always linked; <see cref="Links"/> adds the others (branches).
///
/// <code>
/// { "version": 1,
///   "slots": [ { "at": [0,0,0], "role": "start" }, { "at": [1,0,0], "role": "path" }, { "at": [2,0,0], "role": "exit" } ],
///   "path": [ [0,0,0], [1,0,0], [2,0,0] ],
///   "links": [ { "a": [1,0,0], "b": [1,0,1] } ],
///   "require_tag": [ { "at": [1,0,0], "tag": "corridor" } ],
///   "require_socket": [ { "at": [1,0,0], "face": "north", "socket": "trench" } ],
///   "structures": [ { "index": 1, "count": 1, "region": { "layer": 0, "usable": { "x": [1, 6], "z": [3, 5] }, "avoid_columns": [2] } } ] }
/// </code>
/// </summary>
public sealed class PlanDescription
{
    /// <summary>Gets the schema version this library reads.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the schema version of this document.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets every slot the level uses and why. The roles are <c>start</c>, <c>path</c>, <c>branch</c> and <c>exit</c>.</summary>
    public IReadOnlyList<DescribedSlot> Slots { get; set; } = [];

    /// <summary>Gets or sets the start-to-exit path: the first slot is the start, the last the exit, each next to the one before.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Path { get; set; } = [];

    /// <summary>Gets or sets extra links between neighbouring slots that must connect.</summary>
    public IReadOnlyList<DescribedLink> Links { get; set; } = [];

    /// <summary>Gets or sets slots whose cell must carry a tag.</summary>
    public IReadOnlyList<DescribedTag> RequireTag { get; set; } = [];

    /// <summary>Gets or sets faces whose socket must be of a type.</summary>
    public IReadOnlyList<DescribedSocket> RequireSocket { get; set; } = [];

    /// <summary>Gets or sets the structures to place, by their position in the recipe's list (1-based).</summary>
    public IReadOnlyList<DescribedStructure> Structures { get; set; } = [];
}
