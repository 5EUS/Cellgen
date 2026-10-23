namespace Cellgen.Interchange;

/// <summary>A tag a slot's cell must carry.</summary>
public sealed class DescribedTag
{
    /// <summary>Gets or sets the slot, as <c>[x, y, z]</c>.</summary>
    public IReadOnlyList<int> At { get; set; } = [];

    /// <summary>Gets or sets the tag.</summary>
    public string Tag { get; set; } = string.Empty;
}
