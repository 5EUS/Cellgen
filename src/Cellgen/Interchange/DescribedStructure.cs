namespace Cellgen.Interchange;

/// <summary>A structure to place, and where it may go.</summary>
public sealed class DescribedStructure
{
    /// <summary>Gets or sets the structure's position in the recipe's list, from 1.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets how many to place; absent rolls the structure's own count (its requirement, chance and range).</summary>
    public int? Count { get; set; }

    /// <summary>Gets or sets where it may go; absent means the whole interior of the grid.</summary>
    public DescribedRegion? Region { get; set; }
}
