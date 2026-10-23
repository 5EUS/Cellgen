namespace Cellgen.Generation;

/// <summary>A cell the solver is considering for a spot. Everything in it is fixed before the solve starts, so a policy's answer may be cached.</summary>
/// <param name="Def">The recipe.</param>
/// <param name="Context">The seed, depth, unlock flags and catalogs.</param>
/// <param name="Cell">The cell.</param>
/// <param name="Anchor">Its rotated footprint's minimum slot.</param>
/// <param name="Rotation">Quarter turns.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct CellQuery(LevelData Def, GenContext Context, CellData Cell, Int3 Anchor, int Rotation);
