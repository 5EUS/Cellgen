namespace Cellgen.Generation;

/// <summary>A structure a layout planner is about to roll a count for.</summary>
/// <param name="Def">The recipe.</param>
/// <param name="Context">The seed, depth, unlock flags and catalogs.</param>
/// <param name="Structure">The structure.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct StructureQuery(LevelData Def, GenContext Context, StructureData Structure);
