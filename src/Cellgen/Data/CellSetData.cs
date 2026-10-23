namespace Cellgen;

/// <summary>A biome's pool of cells. A level draws from one or more of these.</summary>
public sealed class CellSetData
{
    /// <summary>Gets a name for diagnostics.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets a multiplier on every cell's weight when a level mixes sets.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Gets the baked cells.</summary>
    public IReadOnlyList<CellData> Cells { get; init; } = [];
}
