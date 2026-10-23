namespace Cellgen;

/// <summary>
/// Per-floor nav marks. The same bits as <c>nav::flags</c> in the extension:
/// append, never renumber.
/// </summary>
[Flags]
public enum NavMarks
{
    /// <summary>An ordinary walkable floor.</summary>
    None = 0,

    /// <summary>The floor exists, but nothing may stand on it.</summary>
    Blocked = 1 << 0,

    /// <summary>Walkable, but enemies route around it when they can.</summary>
    Hazard = 1 << 1,

    /// <summary>Walkable, but never chosen as a spawn point.</summary>
    NoSpawn = 1 << 2,
}
