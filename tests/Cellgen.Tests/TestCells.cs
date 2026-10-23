namespace Cellgen.Tests;

/// <summary>Builds the one-slot cells the synthetic levels are made of.</summary>
internal static class TestCells
{
    /// <summary>Builds a one-slot cell with sockets east and/or west.</summary>
    /// <param name="scene">Its scene path.</param>
    /// <param name="socket">The socket type on its open faces.</param>
    /// <param name="east">Whether the east face is open.</param>
    /// <param name="west">Whether the west face is open.</param>
    /// <param name="spawns">Whether it carries a zone, an item spawn and a prop spawn.</param>
    /// <param name="tags">Its tags.</param>
    /// <returns>The cell.</returns>
    internal static CellData OneSlot(string scene, SocketData socket, bool east, bool west, bool spawns, params string[] tags)
    {
        var sockets = new List<SocketEntryData>();
        if (east)
        {
            sockets.Add(new SocketEntryData { Slot = Int3.Zero, Face = CellFace.East, Socket = socket });
        }

        if (west)
        {
            sockets.Add(new SocketEntryData { Slot = Int3.Zero, Face = CellFace.West, Socket = socket });
        }

        return new CellData
        {
            ScenePath = scene,
            Tags = tags,
            Sockets = sockets,
            AllowedRotations = 1,
            Zones = spawns ? [new ZoneData { NodePath = "Zone", SpawnPoints = [new Vec3(0f, 0f, 0f)] }] : [],
            Items = spawns ? [new ItemSpawnData { NodePath = "Item" }] : [],
            PropSpawns = spawns ? [new PropSpawnData { NodePath = "Prop", PropIds = ["prop_a", "prop_b"], Chance = 1f }] : [],
        };
    }
}
