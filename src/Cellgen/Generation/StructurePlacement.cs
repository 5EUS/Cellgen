namespace Cellgen.Generation;

/// <summary>A structure the macro planner placed: its pieces, and where it joins the path.</summary>
/// <param name="Def">The structure.</param>
/// <param name="Pieces">Its cells, already rotated and anchored.</param>
/// <param name="Entrance">The link between its entrance and the path slot it opens onto.</param>
internal sealed record StructurePlacement(StructureData Def, IReadOnlyList<PlacedPiece> Pieces, SlotEdge Entrance);
