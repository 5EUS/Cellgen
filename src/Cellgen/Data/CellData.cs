namespace Cellgen;

/// <summary>
/// A cell, baked: its footprint, sockets, spawn zones, item spawns and floor,
/// as plain data the generator reads without loading anything engine-side.
/// </summary>
public sealed class CellData
{
    /// <summary>Gets the cell's source (a scene path, for a Godot host). Identifies the cell in plans and fingerprints.</summary>
    public string ScenePath { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the host found the bake out of date with its source. The generator warns and uses it anyway.</summary>
    public bool IsStale { get; init; }

    /// <summary>Gets the slots the cell occupies along X, Y (layers) and Z.</summary>
    public Int3 Footprint { get; init; } = Int3.One;

    /// <summary>Gets the cell's open faces.</summary>
    public IReadOnlyList<SocketEntryData> Sockets { get; init; } = [];

    /// <summary>Gets free-form tags: <c>start</c>, <c>exit</c>, <c>trench</c>, <c>open</c>.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets how often the cell is picked relative to others that fit.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Gets which quarter turns the generator may use, as bits 0-3.</summary>
    public int AllowedRotations { get; init; } = 0b1111;

    /// <summary>Gets the first run depth the cell may appear at.</summary>
    public int MinDepth { get; init; }

    /// <summary>Gets the last run depth the cell may appear at; -1 for no limit.</summary>
    public int MaxDepth { get; init; } = -1;

    /// <summary>Gets the most copies one level may contain; 0 for no limit.</summary>
    public int MaxPerLevel { get; init; }

    /// <summary>Gets the cell's spawn zones.</summary>
    public IReadOnlyList<ZoneData> Zones { get; init; } = [];

    /// <summary>Gets the cell's item spawns.</summary>
    public IReadOnlyList<ItemSpawnData> Items { get; init; } = [];

    /// <summary>Gets the cell's artillery spawns.</summary>
    public IReadOnlyList<ArtillerySpawnData> ArtillerySpawns { get; init; } = [];

    /// <summary>Gets a value indicating whether the cell contains a player start.</summary>
    public bool HasPlayerStart { get; init; }

    /// <summary>Gets a value indicating whether the cell contains a level exit.</summary>
    public bool HasLevelExit { get; init; }

    /// <summary>Gets the nav column size the floor was baked at.</summary>
    public float NavResolution { get; init; } = 1f;

    /// <summary>Gets the nav columns along X and Z.</summary>
    public Int2 NavColumns { get; init; }

    /// <summary>Gets the floors per column.</summary>
    public int NavLayers { get; init; }

    /// <summary>Gets floor heights relative to the cell root, indexed <c>(z * NavColumns.X + x) * NavLayers + layer</c>, highest first, NaN past the last floor.</summary>
    public IReadOnlyList<float> NavHeights { get; init; } = [];

    /// <summary>Gets one <see cref="NavMarks"/> byte per entry of <see cref="NavHeights"/>.</summary>
    public IReadOnlyList<byte> NavMarkBytes { get; init; } = [];

    /// <summary>Gets whether <see cref="Tags"/> contains <paramref name="tag"/>.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>True when present.</returns>
    public bool HasTag(string tag) => this.Tags.Contains(tag, StringComparer.Ordinal);
}
