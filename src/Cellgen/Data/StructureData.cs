namespace Cellgen;

/// <summary>A group of cells placed as one unit, with rules for where: the shop, a field hospital, a command bunker.</summary>
public sealed class StructureData
{
    /// <summary>Gets the stable identity, for diagnostics, saves and the preview.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the structure's cells.</summary>
    public IReadOnlyList<StructurePieceData> Pieces { get; init; } = [];

    /// <summary>Gets whether the structure must, may, or may once unlocked appear.</summary>
    public StructureRequirement Requirement { get; init; } = StructureRequirement.Optional;

    /// <summary>Gets the flag that makes an Unlockable structure eligible; empty for none.</summary>
    public string UnlockFlag { get; init; } = string.Empty;

    /// <summary>Gets the probability an Optional or Unlockable structure is attempted at all.</summary>
    public float Chance { get; init; } = 1f;

    /// <summary>Gets the fewest copies placed when the structure is used.</summary>
    public int MinCount { get; init; } = 1;

    /// <summary>Gets the most copies placed.</summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>Gets the fewest path steps from the start slot to the structure's entrance.</summary>
    public int MinPathDistance { get; init; } = 1;

    /// <summary>Gets the most path steps from the start; -1 for no limit.</summary>
    public int MaxPathDistance { get; init; } = -1;

    /// <summary>Gets the layer the structure sits on; 0 is the surface.</summary>
    public int Layer { get; init; }

    /// <summary>Gets a value indicating whether the entrance must open onto the start-to-exit path rather than a branch.</summary>
    public bool MustBeOnCriticalPath { get; init; }

    /// <summary>Gets a value indicating whether the structure may be turned.</summary>
    public bool AllowRotation { get; init; } = true;
}
