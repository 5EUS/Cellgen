namespace Cellgen;

/// <summary>An artillery spawn as the bake recorded it.</summary>
public sealed class ArtillerySpawnData
{
    /// <summary>Gets the spawn node's path, relative to the cell root.</summary>
    public string NodePath { get; init; } = string.Empty;

    /// <summary>Gets the spawn's position, in cell-local space.</summary>
    public Vec3 Position { get; init; }

    /// <summary>Gets the ids of the pieces the spawn may place. The generator never looks inside them.</summary>
    public IReadOnlyList<string> PieceIds { get; init; } = [];

    /// <summary>Gets the probability the spawn places anything.</summary>
    public float Chance { get; init; } = 0.5f;

    /// <summary>Gets tags the cell must carry one of; empty accepts any.</summary>
    public IReadOnlyList<string> RequiredTags { get; init; } = [];
}
