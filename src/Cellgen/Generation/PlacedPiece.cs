namespace Cellgen.Generation;

/// <summary>A cell variant at a level slot.</summary>
/// <param name="Variant">The cell at its rotation.</param>
/// <param name="Anchor">The rotated footprint's minimum slot, in level space.</param>
internal sealed record PlacedPiece(CellVariant Variant, Int3 Anchor)
{
    /// <summary>Gets the level slots the piece covers.</summary>
    /// <returns>The slots.</returns>
    public IEnumerable<Int3> Covered()
    {
        foreach (Int3 local in this.Variant.Slots)
        {
            yield return this.Anchor + local;
        }
    }

    /// <summary>Gets the socket on a level slot's face, or null.</summary>
    /// <param name="slot">A level slot the piece covers.</param>
    /// <param name="face">The face.</param>
    /// <returns>The socket type, or null if closed.</returns>
    public SocketData? SocketAt(Int3 slot, CellFace face) => this.Variant.SocketAt(slot - this.Anchor, face);
}
