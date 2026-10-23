namespace Cellgen.Generation;

/// <summary>
/// A structure at one rotation: its pieces relative to its own minimum slot,
/// the slots it covers, and its sockets that face outside it.
/// </summary>
internal sealed class StructureLayout
{
    private StructureLayout(List<PlacedPiece> pieces)
    {
        this.Pieces = pieces;
        var slots = new HashSet<Int3>();
        foreach (PlacedPiece piece in pieces)
        {
            slots.UnionWith(piece.Covered());
        }

        var max = Int3.Zero;
        foreach (Int3 slot in slots)
        {
            max = new Int3(Math.Max(max.X, slot.X), Math.Max(max.Y, slot.Y), Math.Max(max.Z, slot.Z));
        }

        this.Footprint = max + Int3.One;
        this.Slots = [.. slots];

        var outer = new List<(Int3, CellFace, SocketData)>();
        foreach (PlacedPiece piece in pieces)
        {
            foreach ((Int3 local, CellFace face) in piece.Variant.OuterFaces)
            {
                Int3 slot = piece.Anchor + local;
                if (!slots.Contains(slot + SlotMath.Offset(face)) && piece.SocketAt(slot, face) is { } socket)
                {
                    outer.Add((slot, face, socket));
                }
            }
        }

        this.OuterSockets = outer;
    }

    /// <summary>Gets the pieces, anchored relative to the structure's minimum slot.</summary>
    public IReadOnlyList<PlacedPiece> Pieces { get; }

    /// <summary>Gets the structure's bounding footprint.</summary>
    public Int3 Footprint { get; }

    /// <summary>Gets every slot the structure covers.</summary>
    public IReadOnlyList<Int3> Slots { get; }

    /// <summary>Gets the open sockets on the structure's outside: its possible entrances.</summary>
    public IReadOnlyList<(Int3 Slot, CellFace Face, SocketData Socket)> OuterSockets { get; }

    /// <summary>The structure at every rotation it allows.</summary>
    /// <param name="def">The structure.</param>
    /// <param name="path">Where it is in the recipe, for diagnostics.</param>
    /// <param name="report">Collects diagnostics.</param>
    /// <returns>One layout per rotation; empty when the structure has no usable pieces.</returns>
    public static List<StructureLayout> All(StructureData def, string path, GenReport report)
    {
        // Unrotated, so the structure's own footprint is known before turning it.
        var authored = new List<(CellTemplate Template, int Rotation, List<Int3> Slots)>();
        var bounds = Int3.Zero;
        for (int i = 0; i < def.Pieces.Count; i++)
        {
            StructurePieceData? piece = def.Pieces[i];
            if (TemplateLibrary.Template(piece?.Cell, 1f, $"{path}.pieces[{i}]", report, rotationMask: 0b1111) is not { } template || piece is null)
            {
                continue;
            }

            var variant = new CellVariant(template, piece.Rotation);
            var slots = new List<Int3>();
            foreach (Int3 local in variant.Slots)
            {
                Int3 slot = piece.Offset + local;
                slots.Add(slot);
                bounds = new Int3(Math.Max(bounds.X, slot.X + 1), Math.Max(bounds.Y, slot.Y + 1), Math.Max(bounds.Z, slot.Z + 1));
            }

            authored.Add((template, piece.Rotation, slots));
        }

        var layouts = new List<StructureLayout>();
        if (authored.Count == 0)
        {
            report.Error(path, $"structure '{def.Id}' has no usable pieces");
            return layouts;
        }

        int turns = def.AllowRotation ? 4 : 1;
        for (int r = 0; r < turns; r++)
        {
            var pieces = new List<PlacedPiece>();
            foreach ((CellTemplate template, int rotation, List<Int3> slots) in authored)
            {
                var min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
                foreach (Int3 slot in slots)
                {
                    Int3 turned = SlotMath.RotateSlot(slot, bounds, r);
                    min = new Int3(Math.Min(min.X, turned.X), Math.Min(min.Y, turned.Y), Math.Min(min.Z, turned.Z));
                }

                pieces.Add(new PlacedPiece(new CellVariant(template, SlotMath.Mod(rotation + r, 4)), min));
            }

            layouts.Add(new StructureLayout(pieces));
        }

        return layouts;
    }
}
