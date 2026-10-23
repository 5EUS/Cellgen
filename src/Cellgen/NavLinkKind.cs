namespace Cellgen;

/// <summary>How a nav link marker is traversed. The numbers are the contract with <c>add_nav_links</c> in <c>rust/src/world/api_level.rs</c>.</summary>
public enum NavLinkKind
{
    /// <summary>Straight up or down: slow, and whoever is on it is exposed.</summary>
    Ladder = 0,

    /// <summary>A leap or a hop across a gap too tall for a step.</summary>
    Jump = 1,

    /// <summary>An instant hop between two places: a hatch, a door, a lift.</summary>
    Portal = 2,
}
