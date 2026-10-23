namespace Cellgen.Generation;

/// <summary>
/// The level's topology before any cell is chosen: which slots are used and
/// why, which neighbouring slots must connect, and where structures sit. A
/// layout planner builds one with the methods below; the cell solver and
/// populator read it and nothing else.
/// </summary>
public sealed class MacroPlan
{
    /// <summary>Gets how many slots the start-to-exit path has so far.</summary>
    public int PathLength => this.CriticalPath.Count;

    /// <summary>Gets the start slot: the first slot appended to the path.</summary>
    public Int3 Start => this.CriticalPath[0];

    /// <summary>Gets the exit slot: the last slot appended to the path.</summary>
    public Int3 Exit => this.CriticalPath[^1];

    /// <summary>Gets the role of every occupied slot.</summary>
    internal Dictionary<Int3, SlotRole> Roles { get; } = [];

    /// <summary>Gets the links that must be open in the finished level.</summary>
    internal HashSet<SlotEdge> Required { get; } = [];

    /// <summary>Gets the start-to-exit path, in order.</summary>
    internal List<Int3> CriticalPath { get; } = [];

    /// <summary>Gets the tag a slot's cell must have, for slots that need one.</summary>
    internal Dictionary<Int3, string> RequiredTags { get; } = [];

    /// <summary>Gets the socket id a slot's cell must have on a face, for faces that need one.</summary>
    internal Dictionary<(Int3 Slot, CellFace Face), string> RequiredSockets { get; } = [];

    /// <summary>Gets the placed structures.</summary>
    internal List<StructurePlacement> Structures { get; } = [];

    /// <summary>Appends a slot to the start-to-exit path. The first slot is the start, the last the exit.</summary>
    /// <param name="slot">The slot. Also <see cref="Occupy"/> it, with a role.</param>
    public void AppendPath(Int3 slot) => this.CriticalPath.Add(slot);

    /// <summary>Gets whether a slot is occupied.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>True when a role was given to it.</returns>
    public bool IsOccupied(Int3 slot) => this.Roles.ContainsKey(slot);

    /// <summary>Gets why a slot is occupied.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="role">The role, when occupied.</param>
    /// <returns>True when the slot is occupied.</returns>
    public bool TryGetRole(Int3 slot, out SlotRole role) => this.Roles.TryGetValue(slot, out role);

    /// <summary>Marks a slot occupied.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="role">Why.</param>
    public void Occupy(Int3 slot, SlotRole role) => this.Roles[slot] = role;

    /// <summary>Requires the cell covering a slot to carry a tag.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="tag">The tag.</param>
    public void RequireTag(Int3 slot, string tag) => this.RequiredTags[slot] = tag;

    /// <summary>Requires the cell covering a slot to offer a socket type on a face.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="face">The face.</param>
    /// <param name="socketId">The socket type's id.</param>
    public void RequireSocket(Int3 slot, CellFace face, string socketId) => this.RequiredSockets[(slot, face)] = socketId;

    /// <summary>Requires two neighbouring slots to connect.</summary>
    /// <param name="a">One slot.</param>
    /// <param name="b">The other.</param>
    public void Link(Int3 a, Int3 b) => this.Required.Add(new SlotEdge(a, b));

    /// <summary>Gets whether two neighbouring slots must connect.</summary>
    /// <param name="a">One slot.</param>
    /// <param name="b">The other.</param>
    /// <returns>True when linked.</returns>
    public bool IsLinked(Int3 a, Int3 b) => this.Required.Contains(new SlotEdge(a, b));
}
