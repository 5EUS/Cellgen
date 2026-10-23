namespace Cellgen;

/// <summary>One cell of a <see cref="StructureData"/>, at a fixed offset and turn within it.</summary>
public sealed class StructurePieceData
{
    /// <summary>Gets the baked cell.</summary>
    public CellData? Cell { get; init; }

    /// <summary>Gets the piece's minimum slot, relative to the structure's.</summary>
    public Int3 Offset { get; init; }

    /// <summary>Gets the piece's quarter turns within the structure, before the structure's own.</summary>
    public int Rotation { get; init; }
}
