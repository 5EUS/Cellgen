namespace Cellgen;

/// <summary>One open face of a cell: which of its slots, which face of that slot, and the connection it offers.</summary>
public sealed class SocketEntryData
{
    /// <summary>Gets the slot within the cell's footprint, from <c>(0,0,0)</c>.</summary>
    public Int3 Slot { get; init; }

    /// <summary>Gets which face of that slot the socket is on.</summary>
    public CellFace Face { get; init; }

    /// <summary>Gets the connection type.</summary>
    public SocketData? Socket { get; init; }
}
