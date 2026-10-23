namespace Cellgen.Generation;

/// <summary>
/// Places whole-grid layouts' structures: anywhere a layout of the structure
/// fits on free slots the caller allows, with one socket opening onto
/// playable ground and none facing the border that can't face a wall.
/// Shared by the whole-grid layouts: <c>WildsPlanner</c> and any planner a host registers.
/// </summary>
public static class StructurePlacer
{
    /// <summary>Tries to place one copy of a structure.</summary>
    /// <param name="def">The structure.</param>
    /// <param name="path">Where it came from, for diagnostics.</param>
    /// <param name="plan">The plan to place it in.</param>
    /// <param name="area">Where it may go.</param>
    /// <param name="rng">The planner's random stream.</param>
    /// <param name="report">Collects diagnostics.</param>
    /// <returns>True when placed.</returns>
    public static bool Place(StructureData def, string path, MacroPlan plan, PlacementArea area, GenRng rng, GenReport report)
    {
        var options = new List<(StructureLayout Layout, Int3 Anchor, SlotEdge Entrance)>();
        foreach (StructureLayout layout in StructureLayout.All(def, path, report))
        {
            for (int x = 1; x < area.Width - 1; x++)
            {
                for (int z = 1; z < area.Depth - 1; z++)
                {
                    var anchor = new Int3(x, area.Layer, z);
                    if (Entrance(layout, anchor, plan, area) is { } entrance)
                    {
                        options.Add((layout, anchor, entrance));
                    }
                }
            }
        }

        if (options.Count == 0)
        {
            return false;
        }

        (StructureLayout chosen, Int3 at, SlotEdge door) = options[rng.Next(options.Count)];
        var pieces = new List<PlacedPiece>();
        foreach (PlacedPiece piece in chosen.Pieces)
        {
            var placed = piece with { Anchor = piece.Anchor + at };
            pieces.Add(placed);
            foreach (Int3 slot in placed.Covered())
            {
                plan.Occupy(slot, SlotRole.Structure);
            }
        }

        plan.Link(door.A, door.B);
        plan.Structures.Add(new StructurePlacement(def, pieces, door));
        return true;
    }

    /// <summary>How many of a structure a level gets: rolled from its requirement, chance, unlock and count range.</summary>
    /// <param name="def">The structure.</param>
    /// <param name="rng">The planner's random stream.</param>
    /// <param name="context">Unlocks.</param>
    /// <returns>How many to place.</returns>
    public static int CountFor(StructureData def, GenRng rng, GenContext context)
    {
        int min = Math.Max(def.Requirement == StructureRequirement.Required ? 1 : 0, def.MinCount);
        int max = Math.Max(min, def.MaxCount);
        return def.Requirement switch
        {
            StructureRequirement.Required => rng.Range(min, max),
            StructureRequirement.Unlockable when !context.UnlockFlags.Contains(def.UnlockFlag, StringComparer.Ordinal) => 0,
            _ => rng.Chance(def.Chance) ? rng.Range(min, max) : 0,
        };
    }

    /// <summary>
    /// Where a structure at this anchor would open onto the level, or null
    /// when it can't go there: on slots the area refuses, or with a socket
    /// facing the border that can't face a wall.
    /// </summary>
    private static SlotEdge? Entrance(StructureLayout layout, Int3 anchor, MacroPlan plan, PlacementArea area)
    {
        foreach (Int3 local in layout.Slots)
        {
            if (!area.CanUse(anchor + local))
            {
                return null;
            }
        }

        SlotEdge? entrance = null;
        foreach ((Int3 local, CellFace face, SocketData socket) in layout.OuterSockets)
        {
            Int3 slot = anchor + local;
            Int3 neighbour = slot + SlotMath.Offset(face);
            bool opensOntoLevel = area.IsPlayable(neighbour)
                && (!plan.Roles.TryGetValue(neighbour, out SlotRole role) || role != SlotRole.Structure);
            if (!opensOntoLevel && !socket.IsBoundaryOk)
            {
                return null;
            }

            if (opensOntoLevel)
            {
                entrance ??= new SlotEdge(slot, neighbour);
            }
        }

        return entrance;
    }
}
