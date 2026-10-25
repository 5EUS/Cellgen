namespace Cellgen.Generation;

/// <summary>
/// <para>
/// Stage A for the wilds layout: a whole-grid wilderness
/// rather than a corridor. It decides only what sockets can't: a closed
/// border ring, structures, the start and an exit far from it, a meandering
/// path between them that is guaranteed open, and a few two-layer landmark
/// slots. Every other slot is filled by the solver with any cell tagged for
/// open ground.
/// </para>
/// <para>
/// Which cells end up together is the cells' business: a group of cells that
/// share an interior socket type no other group accepts can only sit beside
/// each other (or beside their own edge cells, which also carry the shared
/// ground socket), so the group grows as a cluster whose size is set by how
/// heavily its edge cells are weighted.
/// </para>
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="WildsPlanner"/> class.</remarks>
/// <param name="def">The recipe.</param>
/// <param name="context">Depth and unlocks.</param>
/// <param name="rng">This stage's random stream.</param>
/// <param name="report">Collects diagnostics.</param>
internal sealed class WildsPlanner(LevelData def, GenContext context, GenRng rng, GenReport report)
{
    private const int LandmarkSpacing = 5;

    private readonly LevelData def = def;
    private readonly GenContext context = context;
    private readonly GenRng rng = rng;
    private readonly GenReport report = report;
    private readonly MacroPlan plan = new();
    private readonly int width = def.GridSize.X;
    private readonly int depth = def.GridSize.Y;

    /// <summary>Plans the level.</summary>
    /// <param name="failure">Why planning failed, when it did.</param>
    /// <returns>The plan, or null when this recipe or seed can't produce one.</returns>
    public MacroPlan? Plan(out string failure)
    {
        if (this.width < 5 || this.depth < 5)
        {
            failure = $"a {this.width}x{this.depth} grid has no room for a border and an interior";
            return null;
        }

        if (!this.PlaceStructures(out failure))
        {
            return null;
        }

        if (!this.PickEnds(out Int3 start, out Int3 exit, out failure))
        {
            return null;
        }

        List<Int3>? path = this.Route(start, exit);
        if (path is null)
        {
            failure = "structures leave no route from the start to the exit";
            return null;
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                this.plan.Link(path[i - 1], path[i]);
            }

            SlotRole role = SlotRole.Path;
            if (i == 0)
            {
                role = SlotRole.Start;
            }
            else if (i == path.Count - 1)
            {
                role = SlotRole.Exit;
            }

            this.plan.Occupy(path[i], role);
            this.plan.CriticalPath.Add(path[i]);
        }

        this.PlaceLandmarks(path);
        this.Fill();
        failure = string.Empty;
        return this.plan;
    }

    private static int Distance(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

    private bool IsPlayable(Int3 slot) => slot.X > 0 && slot.Z > 0 && slot.X < this.width - 1 && slot.Z < this.depth - 1 && slot.Y == 0;

    private bool IsFree(Int3 slot) => this.IsPlayable(slot) && !this.plan.Roles.ContainsKey(slot);

    private bool PlaceStructures(out string failure)
    {
        var area = new PlacementArea(0, this.width, this.depth, this.IsPlayable, this.IsFree);
        for (int i = 0; i < this.def.Structures.Count; i++)
        {
            if (this.def.Structures[i] is not { } structure)
            {
                continue;
            }

            int wanted = StructurePlacer.CountFor(this.def, structure, this.rng, this.context);
            int placed = 0;
            while (placed < wanted && StructurePlacer.Place(structure, $"structures[{i}]", this.plan, area, this.rng, this.report))
            {
                placed++;
            }

            if (structure.Requirement == StructureRequirement.Required && placed < Math.Max(1, structure.MinCount))
            {
                failure = $"structures[{i}] ('{structure.Id}'): no room with an entrance onto open ground";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    /// <summary>A start anywhere, and an exit at least the recipe's distance away, likelier the farther it is.</summary>
    private bool PickEnds(out Int3 start, out Int3 exit, out string failure)
    {
        var free = new List<Int3>();
        for (int z = 1; z < this.depth - 1; z++)
        {
            for (int x = 1; x < this.width - 1; x++)
            {
                var slot = new Int3(x, 0, z);
                if (this.IsFree(slot))
                {
                    free.Add(slot);
                }
            }
        }

        start = default;
        exit = default;
        if (free.Count < 2)
        {
            failure = "no free interior slots for the start and exit";
            return false;
        }

        start = free[this.rng.Next(free.Count)];
        var far = new List<Int3>();
        var weights = new List<float>();
        foreach (Int3 slot in free)
        {
            int distance = Distance(slot, start);
            if (distance >= this.def.MinStartExitDistance)
            {
                far.Add(slot);
                weights.Add(distance);
            }
        }

        if (far.Count == 0)
        {
            failure = $"no slot is {this.def.MinStartExitDistance} slots from the start at {start}";
            return false;
        }

        exit = far[Math.Max(0, this.rng.PickWeighted(weights))];
        failure = string.Empty;
        return true;
    }

    /// <summary>The cheapest route over free slots when each slot's cost is rolled, so the path meanders and differs per seed.</summary>
    private List<Int3>? Route(Int3 start, Int3 exit)
    {
        var cost = new float[this.width][];
        for (int x = 0; x < this.width; x++)
        {
            cost[x] = new float[this.depth];
            for (int z = 0; z < this.depth; z++)
            {
                cost[x][z] = 1f + (this.rng.NextFloat() * 3f);
            }
        }

        var best = new Dictionary<Int3, float> { [start] = 0f };
        var from = new Dictionary<Int3, Int3>();
        var queue = new PriorityQueue<Int3, float>();
        queue.Enqueue(start, 0f);
        while (queue.TryDequeue(out Int3 at, out float spent))
        {
            if (at == exit)
            {
                break;
            }

            if (spent > best[at])
            {
                continue;
            }

            for (int f = 0; f < 4; f++)
            {
                Int3 next = at + SlotMath.Offset((CellFace)f);
                if (!this.IsPlayable(next) || (this.plan.Roles.ContainsKey(next) && next != exit))
                {
                    continue;
                }

                float total = spent + cost[next.X][next.Z];
                if (!best.TryGetValue(next, out float known) || total < known)
                {
                    best[next] = total;
                    from[next] = at;
                    queue.Enqueue(next, total);
                }
            }
        }

        if (!from.ContainsKey(exit))
        {
            return null;
        }

        var path = new List<Int3> { exit };
        while (path[^1] != start)
        {
            path.Add(from[path[^1]]);
        }

        path.Reverse();
        return path;
    }

    /// <summary>Reserves slots two layers high for the tall cells, apart from each other and from the path's ends.</summary>
    private void PlaceLandmarks(List<Int3> path)
    {
        if (this.def.LandmarkCount <= 0)
        {
            return;
        }

        if (!this.def.AllowedLayers.Contains(1))
        {
            this.report.Warn("landmark_count", "landmarks need layer 1 in allowed_layers; none placed");
            return;
        }

        var options = new List<Int3>();
        for (int z = 1; z < this.depth - 1; z++)
        {
            for (int x = 1; x < this.width - 1; x++)
            {
                var slot = new Int3(x, 0, z);
                if (this.IsFree(slot) && Distance(slot, path[0]) > 2 && Distance(slot, path[^1]) > 2)
                {
                    options.Add(slot);
                }
            }
        }

        var chosen = new List<Int3>();
        while (chosen.Count < this.def.LandmarkCount && options.Count > 0)
        {
            int index = this.rng.Next(options.Count);
            Int3 slot = options[index];
            options.RemoveAt(index);
            if (chosen.TrueForAll(other => Distance(slot, other) >= LandmarkSpacing))
            {
                chosen.Add(slot);
            }
        }

        foreach (Int3 slot in chosen)
        {
            foreach (Int3 layered in new[] { slot, slot + Int3.Up })
            {
                this.plan.Occupy(layered, SlotRole.Branch);
                this.plan.RequireTag(layered, this.def.LandmarkTag);
            }
        }
    }

    /// <summary>Everything not yet planned: the border ring and the open ground inside it.</summary>
    private void Fill()
    {
        for (int z = 0; z < this.depth; z++)
        {
            for (int x = 0; x < this.width; x++)
            {
                var slot = new Int3(x, 0, z);
                if (!this.IsPlayable(slot))
                {
                    this.plan.Occupy(slot, SlotRole.Branch);
                    this.plan.RequireTag(slot, this.def.BorderTag);
                    continue;
                }

                SlotRole? role = this.plan.Roles.TryGetValue(slot, out SlotRole known) ? known : null;
                if (role is null)
                {
                    this.plan.Occupy(slot, SlotRole.Branch);
                }

                if (role is null or SlotRole.Path)
                {
                    this.plan.RequireTag(slot, this.def.GroundTag);
                }
            }
        }
    }
}
