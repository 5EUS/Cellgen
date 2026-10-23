namespace Cellgen.Generation;

/// <summary>A structure a layout planner placed: its pieces, and where it joins the path.</summary>
public sealed class StructurePlacement
{
    /// <summary>Initializes a new instance of the <see cref="StructurePlacement"/> class.</summary>
    /// <param name="def">The structure.</param>
    /// <param name="pieces">Its cells, already rotated and anchored.</param>
    /// <param name="entrance">The link between its entrance and the path slot it opens onto.</param>
    internal StructurePlacement(StructureData def, IReadOnlyList<PlacedPiece> pieces, SlotEdge entrance)
    {
        this.Def = def;
        this.Pieces = pieces;
        this.Entrance = entrance;
    }

    /// <summary>Gets the structure.</summary>
    public StructureData Def { get; }

    /// <summary>Gets the link between its entrance and the path slot it opens onto.</summary>
    public SlotEdge Entrance { get; }

    /// <summary>Gets its cells, already rotated and anchored.</summary>
    internal IReadOnlyList<PlacedPiece> Pieces { get; }
}
