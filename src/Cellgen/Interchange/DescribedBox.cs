namespace Cellgen.Interchange;

/// <summary>A rectangle of slots, both ranges inclusive.</summary>
public sealed class DescribedBox
{
    /// <summary>Gets or sets <c>[min, max]</c> along X.</summary>
    public IReadOnlyList<int> X { get; set; } = [];

    /// <summary>Gets or sets <c>[min, max]</c> along Z.</summary>
    public IReadOnlyList<int> Z { get; set; } = [];
}
