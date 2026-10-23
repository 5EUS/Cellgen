namespace Cellgen.Generation;

/// <summary>Where a structure may go in a whole-grid layout.</summary>
/// <param name="Layer">The layer slots are placed on.</param>
/// <param name="Width">Grid width, in slots.</param>
/// <param name="Depth">Grid depth, in slots.</param>
/// <param name="IsPlayable">Whether a slot is inside the border.</param>
/// <param name="CanUse">Whether a slot is free and allowed for structures.</param>
public sealed record PlacementArea(int Layer, int Width, int Depth, Func<Int3, bool> IsPlayable, Func<Int3, bool> CanUse);
