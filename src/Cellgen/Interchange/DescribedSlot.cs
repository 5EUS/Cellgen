namespace Cellgen.Interchange;

/// <summary>A slot and its role.</summary>
public sealed class DescribedSlot
{
    /// <summary>Gets or sets the slot, as <c>[x, y, z]</c>.</summary>
    public IReadOnlyList<int> At { get; set; } = [];

    /// <summary>Gets or sets the role: <c>start</c>, <c>path</c>, <c>branch</c> or <c>exit</c>.</summary>
    public string Role { get; set; } = string.Empty;
}
