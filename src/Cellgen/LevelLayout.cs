namespace Cellgen;

/// <summary>How the macro planner lays out a level's slots.</summary>
public enum LevelLayout
{
    /// <summary>A winding start-to-exit corridor with side branches; every other slot stays empty.</summary>
    Corridors,

    /// <summary>
    /// A whole-grid battlefield: a border, two trench bands facing each other
    /// across no man's land, the start behind one and the exit behind the
    /// other. Every slot is filled, and each carries a tag its cell must have.
    /// </summary>
    Front,

    /// <summary>
    /// A whole-grid wilderness: a border ring, the start and exit far apart,
    /// a few tall landmark slots, and every other slot filled by the solver,
    /// so the cells' sockets alone decide which groups of cells cluster.
    /// </summary>
    Wilds,
}
