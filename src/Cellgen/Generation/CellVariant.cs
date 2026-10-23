using System.Text;

namespace Cellgen.Generation;

/// <summary>
/// A cell at one rotation: its footprint and its sockets in rotated slot
/// space, ready for the solver to test against neighbours.
/// </summary>
internal sealed class CellVariant
{
    private readonly Dictionary<(Int3 Slot, CellFace Face), SocketData> sockets;

    /// <summary>Initializes a new instance of the <see cref="CellVariant"/> class.</summary>
    /// <param name="template">The cell.</param>
    /// <param name="rotation">Quarter turns.</param>
    public CellVariant(CellTemplate template, int rotation)
    {
        this.Template = template;
        this.Rotation = rotation;
        this.Footprint = SlotMath.RotateFootprint(template.Footprint, rotation);
        this.sockets = [];
        foreach (SocketEntryData? entry in template.Info.Sockets)
        {
            if (entry?.Socket is null)
            {
                continue;
            }

            Int3 slot = SlotMath.RotateSlot(entry.Slot, template.Footprint, rotation);
            this.sockets[(slot, SlotMath.Rotate(entry.Face, rotation))] = entry.Socket;
        }

        var slots = new List<Int3>();
        var outer = new List<(Int3 Slot, CellFace Face)>();
        for (int y = 0; y < this.Footprint.Y; y++)
        {
            for (int z = 0; z < this.Footprint.Z; z++)
            {
                for (int x = 0; x < this.Footprint.X; x++)
                {
                    var slot = new Int3(x, y, z);
                    slots.Add(slot);
                    foreach (CellFace face in SlotMath.Faces)
                    {
                        if (!SlotMath.Contains(this.Footprint, slot + SlotMath.Offset(face)))
                        {
                            outer.Add((slot, face));
                        }
                    }
                }
            }
        }

        this.Slots = slots;
        this.OuterFaces = outer;
    }

    /// <summary>Gets the cell.</summary>
    public CellTemplate Template { get; }

    /// <summary>Gets the quarter turns applied.</summary>
    public int Rotation { get; }

    /// <summary>Gets the rotated footprint.</summary>
    public Int3 Footprint { get; }

    /// <summary>Gets every slot of the rotated footprint, relative to its minimum corner.</summary>
    public IReadOnlyList<Int3> Slots { get; }

    /// <summary>Gets every face on the outside of the footprint, open or closed.</summary>
    public IReadOnlyList<(Int3 Slot, CellFace Face)> OuterFaces { get; }

    /// <summary>Gets or sets this variant's share of its cell's weight. Symmetric cells have fewer distinct variants, each weighted more.</summary>
    public float Weight { get; set; } = 1f;

    /// <summary>Gets whether any slot has a socket on <paramref name="face"/>.</summary>
    /// <param name="face">The face.</param>
    /// <returns>True when one does.</returns>
    public bool HasSocketOn(CellFace face)
    {
        foreach ((Int3 _, CellFace f) in this.sockets.Keys)
        {
            if (f == face)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the socket on a face of a slot, or null if it is closed.</summary>
    /// <param name="slot">The slot, relative to the rotated footprint.</param>
    /// <param name="face">The face, already rotated.</param>
    /// <returns>The socket type, or null.</returns>
    public SocketData? SocketAt(Int3 slot, CellFace face) =>
        this.sockets.TryGetValue((slot, face), out SocketData? socket) ? socket : null;

    /// <summary>
    /// A string identifying the variant's shape and sockets, so rotations of
    /// a symmetric cell that look identical can be merged.
    /// </summary>
    /// <returns>The signature.</returns>
    public string Signature()
    {
        var keys = new List<string>();
        foreach (((Int3 slot, CellFace face), SocketData socket) in this.sockets)
        {
            keys.Add($"{slot.X},{slot.Y},{slot.Z}:{(int)face}={socket.Id}");
        }

        keys.Sort(System.StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.Append(this.Footprint.ToString());
        foreach (string key in keys)
        {
            builder.Append('|').Append(key);
        }

        return builder.ToString();
    }
}
