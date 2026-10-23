namespace Cellgen.Interchange;

/// <summary>Where a structure may be placed in a whole-grid layout.</summary>
public sealed class DescribedRegion
{
    /// <summary>Gets or sets the layer; absent is the first allowed layer.</summary>
    public int? Layer { get; set; }

    /// <summary>Gets or sets the slots that count as open ground for the structure's entrance; absent is the grid inside its border.</summary>
    public DescribedBox? Playable { get; set; }

    /// <summary>Gets or sets the slots the structure itself may occupy; absent is <see cref="Playable"/>.</summary>
    public DescribedBox? Usable { get; set; }

    /// <summary>Gets or sets columns (x values) the structure must keep out of.</summary>
    public IReadOnlyList<int>? AvoidColumns { get; set; }
}
