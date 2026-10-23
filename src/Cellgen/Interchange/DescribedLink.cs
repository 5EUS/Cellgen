namespace Cellgen.Interchange;

/// <summary>A link between two neighbouring slots.</summary>
public sealed class DescribedLink
{
    /// <summary>Gets or sets one slot, as <c>[x, y, z]</c>.</summary>
    public IReadOnlyList<int> A { get; set; } = [];

    /// <summary>Gets or sets the other, as <c>[x, y, z]</c>.</summary>
    public IReadOnlyList<int> B { get; set; } = [];
}
