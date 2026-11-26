namespace Cellgen.Generation;

/// <summary>
/// Gives another layout hilly ground. It lets the inner planner decide which slots exist and how they connect, then rolls an
/// <see cref="ElevationField"/> over them and hands it to the cell solver, which places only cells whose corner heights
/// fit. Neighbouring corners differ by at most one step, so a catalog of every tilt a corner can have is finite; the start,
/// the exit and every structure stand on level pads so they can use ordinary flat cells.
///
/// <para>
/// The ground rolls everywhere: two octaves of value noise, so there are hills and hollows across the whole level rather
/// than a few summits on a plain, and then any corner that stands more than a step from a neighbour is pulled back. A few
/// level plateaus (<c>elevation_plateaus</c>) are the deliberate flat ground, besides the pads.
///
/// Settings, from <see cref="LevelData.LayoutSettings"/>: <c>elevation_step</c> (metres per step, default 4; 0 or less leaves
/// the ground level), <c>elevation_relief</c> (the tallest ground, in steps, default 6), <c>elevation_scale</c> (corners
/// across one coarse hill, default 6; smaller is choppier) and <c>elevation_plateaus</c> (how many 2x2-slot flats; default
/// one per 45 slots). The field is drawn from the request's random stream, after the inner planner's draws.
/// </para>
/// </summary>
/// <param name="id">The id recipes name in <see cref="LevelData.LayoutId"/>.</param>
/// <param name="inner">The planner that lays out the slots.</param>
public sealed class ElevationLayout(string id, ILayoutPlanner inner) : ILayoutPlanner
{
    /// <summary>Gets the setting naming how many metres one elevation step is.</summary>
    public const string StepSetting = "elevation_step";

    /// <summary>Gets the setting naming the tallest ground, in steps.</summary>
    public const string ReliefSetting = "elevation_relief";

    /// <summary>Gets the setting naming how many corners across one coarse hill is.</summary>
    public const string ScaleSetting = "elevation_scale";

    /// <summary>Gets the setting naming how many level plateaus (2x2 slots each) the ground has.</summary>
    public const string PlateausSetting = "elevation_plateaus";

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
        float step = settings.GetFloat(StepSetting, 4f);
        if (step <= 0f)
        {
            return plan;
        }

        int relief = Math.Max(0, settings.GetInt(ReliefSetting, 6));
        int scale = Math.Max(2, settings.GetInt(ScaleSetting, 6));
        int plateaus = settings.GetInt(PlateausSetting, -1);
        ElevationField? field = Roll(plan, request.Def.GroundTag, step, relief, scale, plateaus, request.Rng, out failure);
        if (field is null)
        {
            return null;
        }

        plan.SetElevation(field);
        return plan;
    }

    /// <summary>
    /// Rolls the field the built-in layout uses over a finished plan: rolling ground, level pads under the start, exit,
    /// structures and reserved slots, and a few plateaus. Draws from <paramref name="rng"/>, so call it after every other
    /// draw the layout makes.
    /// </summary>
    /// <param name="plan">The plan, with its slots, structures and required tags set.</param>
    /// <param name="groundTag">The tag of ordinary ground; a slot reserved for any other tag is a pad.</param>
    /// <param name="step">Metres per height step.</param>
    /// <param name="relief">The tallest ground, in steps.</param>
    /// <param name="scale">Corners across one coarse hill.</param>
    /// <param name="plateaus">How many level 2x2-slot plateaus; negative for one per 45 slots.</param>
    /// <param name="rng">The random stream.</param>
    /// <param name="failure">Why no field was made, when null is returned.</param>
    /// <returns>The field, or null when the pads cannot be joined.</returns>
    internal static ElevationField? Roll(MacroPlan plan, string groundTag, float step, int relief, int scale, int plateaus, GenRng rng, out string failure)
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
        int[] heights = Rolling(width, depth, relief, scale, rng);
        List<(int X, int Z)> flats = Plateaus(plan, width, depth, plateaus < 0 ? plan.Roles.Count / 45 : plateaus, rng);
        bool[] pinned = new bool[heights.Length];
        PinPads(plan, groundTag, width, depth, heights, pinned, flats);
        if (!Relax(width, depth, relief, heights, pinned))
        {
            failure = "level pads (start, exit, structures) sit too close together for the terrain to climb between them";
            return null;
        }

        failure = string.Empty;
        return new ElevationField(new Int2(width, depth), step, heights);
    }

    /// <summary>
    /// Two octaves of value noise, integer throughout: a coarse one a hill across (<paramref name="scale"/> corners) carrying
    /// two thirds of the relief and a fine one half as wide carrying the rest. Neighbouring corners can differ by more than
    /// a step here; <see cref="Relax"/> pulls them back.
    /// </summary>
    private static int[] Rolling(int width, int depth, int relief, int scale, GenRng rng)
    {
        int[] coarse = Octave(width, depth, scale, (relief * 2) / 3, rng);
        int[] fine = Octave(width, depth, Math.Max(2, scale / 2), relief - ((relief * 2) / 3), rng);
        var heights = new int[width * depth];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = Math.Clamp(coarse[i] + fine[i], 0, relief);
        }

        return heights;
    }

    /// <summary>Random lattice values from 0 to <paramref name="amplitude"/> every <paramref name="spacing"/> corners, bilinearly interpolated.</summary>
    private static int[] Octave(int width, int depth, int spacing, int amplitude, GenRng rng)
    {
        int lw = (width / spacing) + 2;
        int ld = (depth / spacing) + 2;
        var lattice = new int[lw * ld];
        for (int i = 0; i < lattice.Length; i++)
        {
            lattice[i] = rng.Range(0, Math.Max(0, amplitude));
        }

        var values = new int[width * depth];
        int area = spacing * spacing;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int cx = x / spacing;
                int cz = z / spacing;
                int tx = x % spacing;
                int tz = z % spacing;
                int sum = (lattice[(cz * lw) + cx] * (spacing - tx) * (spacing - tz))
                    + (lattice[(cz * lw) + cx + 1] * tx * (spacing - tz))
                    + (lattice[((cz + 1) * lw) + cx] * (spacing - tx) * tz)
                    + (lattice[((cz + 1) * lw) + cx + 1] * tx * tz);
                values[(z * width) + x] = (sum + (area / 2)) / area;
            }
        }

        return values;
    }

    /// <summary>Picks where the level plateaus go: 2x2 slots clear of the rim, drawn from the random stream (so the count does not change later draws' meaning).</summary>
    private static List<(int X, int Z)> Plateaus(MacroPlan plan, int width, int depth, int count, GenRng rng)
    {
        var flats = new List<(int X, int Z)>();
        if (width < 6 || depth < 6)
        {
            return flats;
        }

        for (int i = 0; i < count; i++)
        {
            // The block's north-west slot, kept off the border ring of slots.
            (int X, int Z) at = (rng.Range(1, width - 4), rng.Range(1, depth - 4));
            if (plan.Roles.ContainsKey(new Int3(at.X, 0, at.Z)))
            {
                flats.Add(at);
            }
        }

        return flats;
    }

    /// <summary>
    /// Levels every corner under a pad slot: the start, the exit, structures, and any slot reserved for a cell that is not
    /// ordinary ground (a required tag other than <paramref name="groundTag"/>: the border, the landmarks). A group of
    /// pads that shares corners stands at the height of its first corner, or at zero when it touches the edge of the
    /// field: the world's rim is the base level, so a border ring is flat and the hills rise inside it.
    /// </summary>
    private static void PinPads(MacroPlan plan, string groundTag, int width, int depth, int[] heights, bool[] pinned, List<(int X, int Z)> plateaus)
    {
        int[] parent = new int[heights.Length];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        var pads = new List<Int3>();
        foreach ((Int3 slot, SlotRole role) in plan.Roles)
        {
            bool reserved = plan.RequiredTags.TryGetValue(slot, out string? tag) && !string.Equals(tag, groundTag, StringComparison.Ordinal);
            if (reserved || role is SlotRole.Start or SlotRole.Exit or SlotRole.Structure)
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

        UnitePlateaus(parent, pinned, width, plateaus);

        LevelGroups(parent, pinned, width, depth, heights);
    }

    /// <summary>A plateau is a 2x2 block of slots, three corners each way, all one level.</summary>
    private static void UnitePlateaus(int[] parent, bool[] pinned, int width, List<(int X, int Z)> plateaus)
    {
        foreach ((int px, int pz) in plateaus)
        {
            int origin = (pz * width) + px;
            for (int dz = 0; dz <= 2; dz++)
            {
                for (int dx = 0; dx <= 2; dx++)
                {
                    int corner = origin + (dz * width) + dx;
                    parent[Find(parent, corner)] = Find(parent, origin);
                    pinned[corner] = true;
                }
            }
        }
    }

    /// <summary>Sets every pinned corner to its group's level: the first corner's height, zero on the rim, then capped by how far apart the groups are.</summary>
    private static void LevelGroups(int[] parent, bool[] pinned, int width, int depth, int[] heights)
    {
        // The group's height is its lowest-indexed corner's: stable however the slots were listed.
        var level = new Dictionary<int, int>();
        for (int i = 0; i < heights.Length; i++)
        {
            if (pinned[i])
            {
                level.TryAdd(Find(parent, i), heights[i]);
            }
        }

        for (int i = 0; i < heights.Length; i++)
        {
            bool rim = pinned[i] && ((i % width) is 0 || (i % width) == width - 1 || (i / width) is 0 || (i / width) == depth - 1);
            if (rim)
            {
                level[Find(parent, i)] = 0;
            }
        }

        CapByDistance(parent, pinned, width, level);
        for (int i = 0; i < heights.Length; i++)
        {
            if (pinned[i])
            {
                heights[i] = level[Find(parent, i)];
            }
        }
    }

    /// <summary>
    /// Lowers pad levels until every pair of pads can be joined by ground that climbs at most a step per corner: no pad
    /// stands higher above another than the corners between them. Levels only fall, so this ends; groups at zero never move.
    /// </summary>
    private static void CapByDistance(int[] parent, bool[] pinned, int width, Dictionary<int, int> level)
    {
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < pinned.Length; i++)
        {
            if (pinned[i])
            {
                int key = Find(parent, i);
                if (!groups.TryGetValue(key, out List<int>? corners))
                {
                    groups[key] = corners = [];
                }

                corners.Add(i);
            }
        }

        List<int> keys = [.. groups.Keys.Order()];
        int[][] gap = [.. keys.Select(_ => new int[keys.Count])];
        for (int a = 0; a < keys.Count; a++)
        {
            for (int b = 0; b < keys.Count; b++)
            {
                gap[a][b] = (a == b) ? 0 : groups[keys[a]].Min(i => groups[keys[b]].Min(j => Math.Abs((i % width) - (j % width)) + Math.Abs((i / width) - (j / width))));
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int a = 0; a < keys.Count; a++)
            {
                for (int b = 0; b < keys.Count; b++)
                {
                    int cap = level[keys[b]] + gap[a][b];
                    if (level[keys[a]] > cap)
                    {
                        level[keys[a]] = cap;
                        changed = true;
                    }
                }
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
