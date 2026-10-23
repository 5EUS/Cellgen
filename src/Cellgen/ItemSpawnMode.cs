namespace Cellgen;

/// <summary>What an item spawn produces.</summary>
public enum ItemSpawnMode
{
    /// <summary>A free pickup, rolled against the level's loot budget.</summary>
    Loot,

    /// <summary>A priced shop offer, rolled from the spawn's own table and outside the loot budget.</summary>
    Shop,

    /// <summary>A free pickup rolled by <c>Chance</c> alone, outside the loot budget: ammo lying around.</summary>
    Ammo,
}
