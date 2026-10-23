namespace Cellgen.Generation;

/// <summary>
/// The level's topology before any cell is chosen: which slots are used and
/// why, which neighbouring slots must connect, and where structures sit.
/// </summary>
internal sealed class MacroPlan
{
    /// <summary>Gets the role of every occupied slot.</summary>
    public Dictionary<Int3, SlotRole> Roles { get; } = [];

    /// <summary>Gets the links that must be open in the finished level.</summary>
    public HashSet<SlotEdge> Required { get; } = [];

    /// <summary>Gets the start-to-exit path, in order.</summary>
    public List<Int3> CriticalPath { get; } = [];

    /// <summary>Gets the tag a slot's cell must have, for slots that need one.</summary>
    public Dictionary<Int3, string> RequiredTags { get; } = [];

    /// <summary>Gets the socket id a slot's cell must have on a face, for faces that need one.</summary>
    public Dictionary<(Int3 Slot, CellFace Face), string> RequiredSockets { get; } = [];

    /// <summary>Gets the placed structures.</summary>
    public List<StructurePlacement> Structures { get; } = [];

    /// <summary>Gets the start slot.</summary>
    public Int3 Start => this.CriticalPath[0];

    /// <summary>Gets the exit slot.</summary>
    public Int3 Exit => this.CriticalPath[^1];

    /// <summary>Marks a slot occupied.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="role">Why.</param>
    public void Occupy(Int3 slot, SlotRole role) => this.Roles[slot] = role;

    /// <summary>Requires the cell covering a slot to carry a tag.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="tag">The tag.</param>
    public void RequireTag(Int3 slot, string tag) => this.RequiredTags[slot] = tag;

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
