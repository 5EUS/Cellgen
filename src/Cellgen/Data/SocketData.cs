namespace Cellgen;

/// <summary>
/// What a slot face connects to: <c>trench</c>, <c>field_open</c>,
/// <c>tunnel</c>. Compatibility is by <see cref="Id"/>, not by object, and it
/// is symmetric: either side listing the other is enough.
/// </summary>
public sealed class SocketData
{
    /// <summary>Gets the stable identity the generator compares.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the other socket types this one may face. A type always matches itself.</summary>
    public IReadOnlyList<SocketData> CompatibleWith { get; init; } = [];

    /// <summary>Gets a value indicating whether this socket may face nothing: the level's edge, an empty slot, or a closed face.</summary>
    public bool IsBoundaryOk { get; init; }

    /// <summary>Gets whether this socket may face <paramref name="other"/>.</summary>
    /// <param name="other">The socket on the other side of the face.</param>
    /// <returns>True when either type lists the other, or they are the same type.</returns>
    public bool Accepts(SocketData other)
    {
        return SameId(this, other) || Lists(this, other) || Lists(other, this);
    }

    private static bool Lists(SocketData owner, SocketData candidate)
    {
        foreach (SocketData? listed in owner.CompatibleWith)
        {
            if (listed is not null && SameId(listed, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameId(SocketData a, SocketData b) => string.Equals(a.Id, b.Id, StringComparison.Ordinal);
}
