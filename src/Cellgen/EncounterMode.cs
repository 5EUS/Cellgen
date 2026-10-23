namespace Cellgen;

/// <summary>
/// How a spawn zone releases its encounter. The values are the extension's
/// wire format for <c>arm_encounter</c> (see <c>rust/src/encounter.rs</c>):
/// append, never renumber.
/// </summary>
public enum EncounterMode
{
    /// <summary>Every enemy stands dormant from the start, and the zone wakes together.</summary>
    PrePlaced = 0,

    /// <summary>Nothing exists until the player enters the trigger; then the groups release at their pace.</summary>
    Triggered = 1,

    /// <summary>As <see cref="Triggered"/>, followed by the encounter's reinforcement waves.</summary>
    Reinforcement = 2,
}
