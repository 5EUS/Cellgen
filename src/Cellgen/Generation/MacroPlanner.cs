namespace Cellgen.Generation;

/// <summary>
/// Stage A: decides the level's topology - the start-to-exit path, where
/// structures go, and side branches - without choosing any cell.
///
/// Topology first is what keeps the solver honest: it only has to find
/// cells that realize a connected plan, rather than hope a connected level
/// emerges from local socket matching.
/// </summary>
internal sealed class MacroPlanner
{
    private readonly LevelData def;
    private readonly TemplateLibrary library;
    private readonly GenContext context;
    private readonly GenRng rng;
    private readonly GenReport report;
    private readonly HashSet<int> layers;
    private readonly MacroPlan plan = new();
    private readonly Dictionary<Int3, int> pathIndex = [];
    private readonly List<Int3> branchSlots = [];
    private readonly HashSet<Int3> vertical = [];
    private readonly Dictionary<StructureData, int> wanted = [];
    private readonly Dictionary<StructureData, int> placed = [];
    private bool branchesGrown;

    /// <summary>Initializes a new instance of the <see cref="MacroPlanner"/> class.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="library">What can be placed.</param>
    /// <param name="context">Depth and unlocks.</param>
    /// <param name="rng">This stage's random stream.</param>
    /// <param name="report">Collects diagnostics.</param>
    public MacroPlanner(LevelData def, TemplateLibrary library, GenContext context, GenRng rng, GenReport report)
    {
        this.def = def;
        this.library = library;
        this.context = context;
        this.rng = rng;
        this.report = report;
        this.layers = [.. def.AllowedLayers];
        if (this.layers.Count == 0)
        {
            this.layers.Add(0);
        }
    }

    /// <summary>Plans the level.</summary>
    /// <param name="failure">Why planning failed, when it did.</param>
    /// <returns>The plan, or null when this seed can't produce one.</returns>
    public MacroPlan? Plan(out string failure)
    {
        if (!this.CarveCriticalPath(out failure))
        {
            return null;
        }

        // Structures open onto the critical path where they can; any that
        // found no room get a second chance off a branch once branches exist.
        this.PlaceStructures();
        this.GrowBranches();
        this.branchesGrown = true;
        this.PlaceStructures();
        return this.RequiredStructuresPlaced(out failure) ? this.plan : null;
    }

    /// <summary>How many of a structure a level gets: rolled from its requirement, chance, unlock and count range.</summary>
    /// <param name="def">The structure.</param>
    /// <param name="rng">The planner's random stream.</param>
    /// <param name="context">Unlocks.</param>
    /// <returns>How many to place.</returns>
    internal static int StructureCount(StructureData def, GenRng rng, GenContext context)
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

    private bool InBounds(Int3 slot) =>
        slot.X >= 0 && slot.Z >= 0 && slot.X < this.def.GridSize.X && slot.Z < this.def.GridSize.Y && this.layers.Contains(slot.Y);

    private bool IsFree(Int3 slot) => this.InBounds(slot) && !this.plan.Roles.ContainsKey(slot);

    private int FreeHorizontalNeighbours(Int3 slot)
    {
        int free = 0;
        for (int f = 0; f < 4; f++)
        {
            free += this.IsFree(slot + SlotMath.Offset((CellFace)f)) ? 1 : 0;
        }

        return free;
    }

    // ------------------------- critical path -------------------------
    private bool CarveCriticalPath(out string failure)
    {
        int top = int.MinValue;
        foreach (int layer in this.layers)
        {
            top = Math.Max(top, layer);
        }

        int length = this.rng.Range(Math.Max(2, this.def.CriticalPathMin), Math.Max(2, this.def.CriticalPathMax));
        var start = new Int3(this.rng.Next(this.def.GridSize.X), top, this.rng.Next(this.def.GridSize.Y));
        this.AddPathSlot(start, SlotRole.Start);

        CellFace? heading = null;
        List<CellFace>? forced = null;
        while (this.plan.CriticalPath.Count < length)
        {
            Int3 current = this.plan.CriticalPath[^1];
            int remaining = length - this.plan.CriticalPath.Count;

            // Never the first or last step - start and exit cells are flat -
            // and never twice running.
            bool tryVertical = forced is null && heading is not null && this.library.CanChangeLayer
                && this.plan.CriticalPath.Count >= 2 && remaining >= 2
                && this.rng.Chance(this.def.LayerChangeChance);
            CellFace? step = null;
            List<CellFace>? continuations = null;
            if (tryVertical && this.PickVertical(current, heading!.Value) is { } verticalMove)
            {
                (step, continuations) = verticalMove;
            }

            step ??= (forced is null) ? this.PickHorizontal(current, heading) : this.PickForced(current, forced);
            if (step is not { } face)
            {
                failure = $"the critical path ran out of room after {this.plan.CriticalPath.Count} of {length} slots";
                return false;
            }

            Int3 next = current + SlotMath.Offset(face);
            this.plan.Link(current, next);
            this.AddPathSlot(next, SlotRole.Path);
            forced = continuations;
            if (SlotMath.IsHorizontal(face))
            {
                heading = face;
            }
            else
            {
                this.vertical.Add(current);
                this.vertical.Add(next);
            }
        }

        this.plan.Occupy(this.plan.Exit, SlotRole.Exit);
        failure = string.Empty;
        return true;
    }

    private void AddPathSlot(Int3 slot, SlotRole role)
    {
        this.pathIndex[slot] = this.plan.CriticalPath.Count;
        this.plan.CriticalPath.Add(slot);
        this.plan.Occupy(slot, role);
    }

    /// <summary>
    /// A change of layer, only where the cells can realize it: the slot
    /// across must be free, and so must a slot to leave it by in a heading
    /// some cell allows (a straight shaft only allows straight on).
    /// </summary>
    /// <returns>The vertical face and the headings the next step may take, or null.</returns>
    private (CellFace Face, List<CellFace> Continuations)? PickVertical(Int3 current, CellFace heading)
    {
        var options = new List<(CellFace, List<CellFace>)>();
        foreach (CellFace face in (CellFace[])[CellFace.Down, CellFace.Up])
        {
            Int3 next = current + SlotMath.Offset(face);
            if (!this.IsFree(next))
            {
                continue;
            }

            List<CellFace> continuations = this.library.TurnsAcross(face)
                .Select(turn => (CellFace)SlotMath.Mod((int)heading + turn, 4))
                .Where(exit => this.IsFree(next + SlotMath.Offset(exit)))
                .ToList();
            if (continuations.Count > 0)
            {
                options.Add((face, continuations));
            }
        }

        return (options.Count == 0) ? null : options[this.rng.Next(options.Count)];
    }

    private CellFace? PickForced(Int3 current, List<CellFace> forced)
    {
        var free = forced.Where(face => this.IsFree(current + SlotMath.Offset(face))).ToList();
        return (free.Count == 0) ? null : free[this.rng.Next(free.Count)];
    }

    /// <summary>
    /// A horizontal step, preferring to keep going straight (corridors read
    /// better than drunk walks) and away from dead ends.
    /// </summary>
    private CellFace? PickHorizontal(Int3 current, CellFace? heading)
    {
        var faces = new List<CellFace>(4);
        var weights = new List<float>(4);
        for (int f = 0; f < 4; f++)
        {
            var face = (CellFace)f;
            Int3 next = current + SlotMath.Offset(face);
            if (!this.IsFree(next))
            {
                continue;
            }

            float weight = (face == heading) ? 2.5f : 1f;

            faces.Add(face);
            weights.Add(weight * (1 + this.FreeHorizontalNeighbours(next)));
        }

        int pick = this.rng.PickWeighted(weights);
        return (pick < 0) ? null : faces[pick];
    }

    // ------------------------- branches -------------------------
    private void GrowBranches()
    {
        for (int b = 0; b < this.def.BranchCount; b++)
        {
            var roots = new List<Int3>();
            for (int i = 1; i < this.plan.CriticalPath.Count - 1; i++)
            {
                this.AddIfRoot(roots, this.plan.CriticalPath[i]);
            }

            foreach (Int3 slot in this.branchSlots)
            {
                this.AddIfRoot(roots, slot);
            }

            if (roots.Count == 0)
            {
                return;
            }

            Int3 current = roots[this.rng.Next(roots.Count)];
            int length = this.rng.Range(Math.Max(1, this.def.BranchLengthMin), Math.Max(1, this.def.BranchLengthMax));
            CellFace? heading = null;
            for (int step = 0; step < length; step++)
            {
                if (this.PickHorizontal(current, heading) is not { } face)
                {
                    break;
                }

                Int3 next = current + SlotMath.Offset(face);
                this.plan.Link(current, next);
                this.plan.Occupy(next, SlotRole.Branch);
                this.pathIndex[next] = this.pathIndex[current] + 1;
                this.branchSlots.Add(next);
                current = next;
                heading = face;
            }
        }
    }

    private void AddIfRoot(List<Int3> roots, Int3 slot)
    {
        // A shaft's slots are spoken for: it has exactly the sockets its
        // layer change uses.
        if (this.plan.Roles.TryGetValue(slot, out SlotRole role) && role is SlotRole.Path or SlotRole.Branch
            && !this.vertical.Contains(slot) && this.FreeHorizontalNeighbours(slot) > 0)
        {
            roots.Add(slot);
        }
    }

    // ------------------------- structures -------------------------

    /// <summary>Places as many of each structure as it still wants and there is room for.</summary>
    private void PlaceStructures()
    {
        for (int i = 0; i < this.def.Structures.Count; i++)
        {
            if (this.def.Structures[i] is not { } structure || (this.branchesGrown && structure.MustBeOnCriticalPath))
            {
                continue;
            }

            if (!this.wanted.TryGetValue(structure, out int count))
            {
                count = StructureCount(structure, this.rng, this.context);
                this.wanted[structure] = count;
            }

            while (this.placed.GetValueOrDefault(structure) < count && this.PlaceStructure(structure, $"structures[{i}]"))
            {
                this.placed[structure] = this.placed.GetValueOrDefault(structure) + 1;
            }
        }
    }

    private bool RequiredStructuresPlaced(out string failure)
    {
        for (int i = 0; i < this.def.Structures.Count; i++)
        {
            if (this.def.Structures[i] is { Requirement: StructureRequirement.Required } structure
                && this.placed.GetValueOrDefault(structure) < Math.Max(1, structure.MinCount))
            {
                failure = $"structures[{i}] ('{structure.Id}'): no room with an entrance onto the path";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private bool PlaceStructure(StructureData def, string path)
    {
        List<StructureLayout> layouts = StructureLayout.All(def, path, this.report);
        var options = new List<(StructureLayout Layout, Int3 Anchor, SlotEdge Entrance)>();
        foreach (StructureLayout layout in layouts)
        {
            for (int x = 0; x <= this.def.GridSize.X - layout.Footprint.X; x++)
            {
                for (int z = 0; z <= this.def.GridSize.Y - layout.Footprint.Z; z++)
                {
                    this.CollectPlacements(layout, new Int3(x, def.Layer, z), def, options);
                }
            }
        }

        if (options.Count == 0)
        {
            return false;
        }

        (StructureLayout chosen, Int3 anchor, SlotEdge entrance) = options[this.rng.Next(options.Count)];
        var pieces = new List<PlacedPiece>();
        foreach (PlacedPiece piece in chosen.Pieces)
        {
            var placedPiece = piece with { Anchor = piece.Anchor + anchor };
            pieces.Add(placedPiece);
            foreach (Int3 slot in placedPiece.Covered())
            {
                this.plan.Occupy(slot, SlotRole.Structure);
            }
        }

        this.plan.Required.Add(entrance);
        this.plan.Structures.Add(new StructurePlacement(def, pieces, entrance));
        return true;
    }

    /// <summary>Adds an option for every entrance this anchor would give the structure.</summary>
    private void CollectPlacements(StructureLayout layout, Int3 anchor, StructureData def, List<(StructureLayout Layout, Int3 Anchor, SlotEdge Entrance)> options)
    {
        foreach (Int3 local in layout.Slots)
        {
            if (!this.IsFree(anchor + local))
            {
                return;
            }
        }

        var entrances = new List<SlotEdge>();
        foreach ((Int3 local, CellFace face, SocketData socket) in layout.OuterSockets)
        {
            Int3 slot = anchor + local;
            Int3 neighbour = slot + SlotMath.Offset(face);
            if (!this.InBounds(neighbour))
            {
                if (!socket.IsBoundaryOk)
                {
                    return; // an open socket facing off the map
                }

                continue;
            }

            if (this.IsEntrance(neighbour, def))
            {
                entrances.Add(new SlotEdge(slot, neighbour));
            }
        }

        foreach (SlotEdge entrance in entrances)
        {
            options.Add((layout, anchor, entrance));
        }
    }

    private bool IsEntrance(Int3 neighbour, StructureData def)
    {
        // Start and exit cells have no spare socket, and neither does a shaft.
        bool onPath = this.plan.Roles.TryGetValue(neighbour, out SlotRole role)
            && (role == SlotRole.Path || (role == SlotRole.Branch && !def.MustBeOnCriticalPath));
        if (!onPath || this.vertical.Contains(neighbour))
        {
            return false;
        }

        int distance = this.pathIndex[neighbour];
        return distance >= def.MinPathDistance && (def.MaxPathDistance < 0 || distance <= def.MaxPathDistance);
    }
}
