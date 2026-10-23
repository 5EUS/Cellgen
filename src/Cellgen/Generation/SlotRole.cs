namespace Cellgen.Generation;

/// <summary>Why the macro planner occupied a slot.</summary>
public enum SlotRole
{
    /// <summary>On the start-to-exit path.</summary>
    Path,

    /// <summary>On a side branch.</summary>
    Branch,

    /// <summary>Part of a structure, already filled.</summary>
    Structure,

    /// <summary>The start of the path; needs a cell with the recipe's start tag.</summary>
    Start,

    /// <summary>The end of the path; needs a cell with the recipe's exit tag.</summary>
    Exit,
}
