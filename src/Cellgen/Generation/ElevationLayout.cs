namespace Cellgen.Generation;

/// <summary>
/// Gives another layout hilly ground. It lets the inner planner decide which slots exist and how they connect, then rolls an
/// <see cref="ElevationField"/> over them and hands it to the cell solver, which places only cells whose corner heights
/// fit. Neighbouring corners differ by at most one step, so a catalog of every tilt a corner can have is finite; the start,
/// the exit and every structure stand on level pads so they can use ordinary flat cells.
///
/// <para>
/// Settings, from <see cref="LevelData.LayoutSettings"/>: <c>elevation_step</c> (metres per step, default 4; 0 or less leaves
/// the ground level), <c>elevation_relief</c> (the tallest ground, in steps, default 6) and <c>elevation_peaks</c> (how many
/// summits, default 3). The field is drawn from the request's random stream, after the inner planner's draws.
/// </para>
/// </summary>
/// <param name="id">The id recipes name in <see cref="LevelData.LayoutId"/>.</param>
/// <param name="inner">The planner that lays out the slots.</param>
public sealed class ElevationLayout(string id, ILayoutPlanner inner) : ILayoutPlanner
{
    private readonly ILayoutPlanner inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public MacroPlan? Plan(LayoutRequest request, out string failure)
    {
        ArgumentNullException.ThrowIfNull(request);
        MacroPlan? plan = this.inner.Plan(request, out failure);
        if (plan is null)
        {
            return null;
        }

        LayoutSettings settings = request.Def.LayoutSettings;
        float step = settings.GetFloat("elevation_step", 4f);
        if (step <= 0f)
        {
            return plan;
        }

        int relief = Math.Max(0, settings.GetInt("elevation_relief", 6));
        int peaks = Math.Max(1, settings.GetInt("elevation_peaks", 3));
        ElevationField? field = BuildField(plan, step, relief, peaks, request.Rng, out failure);
        if (field is null)
        {
            return null;
        }

        plan.SetElevation(field);
        return plan;
    }

    private static ElevationField? BuildField(MacroPlan plan, float step, int relief, int peaks, GenRng rng, out string failure)
    {
        int maxX = 0;
        int maxZ = 0;
        foreach (Int3 slot in plan.Roles.Keys)
        {
            if (slot.X < 0 || slot.Z < 0)
            {
                failure = $"slot {slot} is outside the grid; elevation needs non-negative slot columns";
                return null;
            }

            maxX = Math.Max(maxX, slot.X);
            maxZ = Math.Max(maxZ, slot.Z);
        }

        int width = maxX + 2;
        int depth = maxZ + 2;
        int[] heights = Summits(width, depth, relief, peaks, rng);
        bool[] pinned = new bool[heights.Length];
        PinPads(plan, width, heights, pinned);
        if (!Relax(width, depth, relief, heights, pinned))
        {
            failure = "level pads (start, exit, structures) sit too close together for the terrain to climb between them";
            return null;
        }

        failure = string.Empty;
        return new ElevationField(new Int2(width, depth), step, heights);
    }

    /// <summary>Takes each corner's height from the tallest of a few cones. A cone falls one step per <c>r</c> corners, so no two neighbouring corners differ by more than one.</summary>
    private static int[] Summits(int width, int depth, int relief, int peaks, GenRng rng)
    {
        var summits = new (int X, int Z, int Height, int Run)[peaks];
        for (int i = 0; i < peaks; i++)
        {
            int top = (relief == 0) ? 0 : rng.Range((relief + 1) / 2, relief);
            summits[i] = (rng.Next(width), rng.Next(depth), top, rng.Range(1, 2));
        }

        var heights = new int[width * depth];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int best = 0;
                foreach ((int sx, int sz, int top, int run) in summits)
                {
                    best = Math.Max(best, top - ((Math.Abs(x - sx) + Math.Abs(z - sz)) / run));
                }

                heights[(z * width) + x] = best;
            }
        }

        return heights;
    }

    /// <summary>Levels every corner under a pad slot, each connected group of pads to the height of its first corner.</summary>
    private static void PinPads(MacroPlan plan, int width, int[] heights, bool[] pinned)
    {
        int[] parent = new int[heights.Length];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        var pads = new List<Int3>();
        foreach ((Int3 slot, SlotRole role) in plan.Roles)
        {
            if (role is SlotRole.Start or SlotRole.Exit or SlotRole.Structure)
            {
                pads.Add(slot);
            }
        }

        pads.Sort(static (a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
        foreach (Int3 slot in pads)
        {
            int first = (slot.Z * width) + slot.X;
            foreach (int corner in new[] { first + 1, first + width, first + width + 1 })
            {
                parent[Find(parent, corner)] = Find(parent, first);
            }

            foreach (int corner in new[] { first, first + 1, first + width, first + width + 1 })
            {
                pinned[corner] = true;
            }
        }

        // The group's height is its lowest-indexed corner's: stable however the slots were listed.
        var level = new Dictionary<int, int>();
        for (int i = 0; i < heights.Length; i++)
        {
            if (pinned[i] && level.TryAdd(Find(parent, i), heights[i]))
            {
                continue;
            }
        }

        for (int i = 0; i < heights.Length; i++)
        {
            if (pinned[i])
            {
                heights[i] = level[Find(parent, i)];
            }
        }
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    /// <summary>Pulls unpinned corners toward their neighbours until none differs from a neighbour by more than one. Fails when two pinned corners need more than that.</summary>
    private static bool Relax(int width, int depth, int relief, int[] heights, bool[] pinned)
    {
        int passes = ((width + depth) * (relief + 1)) + 8;
        for (int pass = 0; pass < passes; pass++)
        {
            bool changed = false;
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int here = (z * width) + x;
                    if (x + 1 < width)
                    {
                        changed |= Settle(heights, pinned, here, here + 1, out bool stuck);
                        if (stuck)
                        {
                            return false;
                        }
                    }

                    if (z + 1 < depth)
                    {
                        changed |= Settle(heights, pinned, here, here + width, out bool stuck);
                        if (stuck)
                        {
                            return false;
                        }
                    }
                }
            }

            if (!changed)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Settle(int[] heights, bool[] pinned, int a, int b, out bool stuck)
    {
        stuck = false;
        int gap = heights[a] - heights[b];
        if (Math.Abs(gap) <= 1)
        {
            return false;
        }

        if (pinned[a] && pinned[b])
        {
            stuck = true;
            return false;
        }

        // Move the free corner; if both are free, bring the taller one down.
        bool moveA = !pinned[a] && (pinned[b] || gap > 0);
        if (moveA)
        {
            heights[a] = heights[b] + Math.Sign(gap);
        }
        else
        {
            heights[b] = heights[a] - Math.Sign(gap);
        }

        return true;
    }
}
