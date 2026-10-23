namespace Cellgen;

/// <summary>Whether a level must, may, or may only once unlocked, contain a structure.</summary>
public enum StructureRequirement
{
    /// <summary>Every level built from the recipe has at least <c>MinCount</c>; a level without it fails validation.</summary>
    Required,

    /// <summary>Placed with probability <c>Chance</c>, when there is room.</summary>
    Optional,

    /// <summary>As <see cref="Optional"/>, but only once the run's unlock flags contain <c>UnlockFlag</c>.</summary>
    Unlockable,
}
