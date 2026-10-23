namespace Cellgen.Interchange;

/// <summary>A socket type a slot's face must have.</summary>
public sealed class DescribedSocket
{
    /// <summary>Gets or sets the slot, as <c>[x, y, z]</c>.</summary>
    public IReadOnlyList<int> At { get; set; } = [];

    /// <summary>Gets or sets the face: <c>north</c>, <c>east</c>, <c>south</c>, <c>west</c>, <c>up</c> or <c>down</c>.</summary>
    public string Face { get; set; } = string.Empty;

    /// <summary>Gets or sets the socket type's id.</summary>
    public string Socket { get; set; } = string.Empty;
}
