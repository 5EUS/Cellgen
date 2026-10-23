namespace Cellgen.Generation;

/// <summary>
/// Stage A for <see cref="LevelLayout.Front"/>: a whole-grid battlefield
/// rather than a corridor. North to south the grid is a border ring, the
/// enemy's trench band, no man's land, and the player's trench band. In each
/// band a front line bends between the two rows nearest no man's land from
/// one side of the map to the other, communication trenches run back from it
/// (one to the start or the exit), and saps poke out into no man's land.
///
/// Every slot is occupied and carries the tag its cell must have (the
/// constants below), so trench cells only go on the trench network; sockets
/// do the rest - a trench face must meet a trench face - which turns each
/// trench cell to follow the line. Structures sit in the player's rear rows.
/// The critical path runs start, communication trench, straight across no
/// man's land, along the enemy line, and back to the exit.
/// </summary>
internal sealed class FrontPlanner
{
    /// <summary>The perimeter: a cell closed on every face.</summary>
    public const string BorderTag = "border";

    /// <summary>Open ground between the two trench bands.</summary>
    public const string NoMansLandTag = "no_mans_land";

    /// <summary>The player's trench network: front line, communication trenches, saps.</summary>
    public const string FriendlyTrenchTag = "friendly_trench";

    /// <summary>Open ground in the player's band.</summary>
    public const string FriendlyGroundTag = "friendly_ground";

    /// <summary>The enemy's trench network.</summary>
    public const string EnemyTrenchTag = "enemy_trench";

    /// <summary>Open ground in the enemy's band.</summary>
    public const string EnemyGroundTag = "enemy_ground";

    /// <summary>The socket a trench crosses a face by.</summary>
    public const string TrenchSocket = "trench";

    /// <summary>The socket every other face of a trench cell has.</summary>
    public const string GroundSocket = "front_ground";

    private const int LineRows = 2;

    private readonly LevelData def;
    private readonly GenContext context;
    private readonly GenRng rng;
    private readonly GenReport report;
    private readonly MacroPlan plan = new();
    private readonly List<Int3> network = [];
    private readonly HashSet<SlotEdge> trenches = [];
    private readonly int width;
    private readonly int depth;
    private readonly int layer;

    /// <summary>Initializes a new instance of the <see cref="FrontPlanner"/> class.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">Depth and unlocks.</param>
    /// <param name="rng">This stage's random stream.</param>
    /// <param name="report">Collects diagnostics.</param>
    public FrontPlanner(LevelData def, GenContext context, GenRng rng, GenReport report)
    {
        this.def = def;
        this.context = context;
        this.rng = rng;
        this.report = report;
        this.width = def.GridSize.X;
        this.depth = def.GridSize.Y;
        this.layer = (def.AllowedLayers.Count > 0) ? def.AllowedLayers.Max() : 0;
    }

    /// <summary>Plans the level.</summary>
    /// <param name="failure">Why planning failed, when it did.</param>
    /// <returns>The plan, or null when this recipe or seed can't produce one.</returns>
    public MacroPlan? Plan(out string failure)
    {
        int rows = Math.Max(LineRows + 1, this.def.TrenchBandRows);
        int noMansLand = this.depth - 2 - (2 * rows);
        if (this.width < 5 || noMansLand < 1)
        {
            failure = $"a {this.width}x{this.depth} grid has no room for a border, two {rows}-row trench bands and no man's land";
            return null;
        }

        var friendly = new Band(FriendlyTrenchTag, FriendlyGroundTag, this.depth - 1 - rows, +1, rows);
        var enemy = new Band(EnemyTrenchTag, EnemyGroundTag, rows, -1, rows);

        var start = new Int3(this.rng.Range(1, this.width - 2), this.layer, friendly.Back);
        var exit = new Int3(this.PickExitColumn(start.X), this.layer, enemy.Back);
        this.plan.Occupy(start, SlotRole.Start);
        this.plan.Occupy(exit, SlotRole.Exit);
        this.network.Add(start);
        this.network.Add(exit);
        friendly.Columns.Add(start.X);
        enemy.Columns.Add(exit.X);

        if (!this.PlaceStructures(friendly, out failure))
        {
            return null;
        }

        this.CarveLine(friendly);
        this.CarveLine(enemy);
        List<Int3> fromStart = this.Communication(friendly, start);
        List<Int3> fromExit = this.Communication(enemy, exit);
        this.ExtraCommunication(friendly);
        this.ExtraCommunication(enemy);
        if (noMansLand >= 3)
        {
            this.DigSaps(friendly);
            this.DigSaps(enemy);
        }

        this.Fill(friendly, enemy);
        this.CriticalPath(fromStart, fromExit, enemy);
        this.RequireTrenchSockets();
        failure = string.Empty;
        return this.plan;
    }

    private bool IsPlayable(Int3 slot) => slot.X > 0 && slot.Z > 0 && slot.X < this.width - 1 && slot.Z < this.depth - 1;

    private bool IsFree(Int3 slot) => this.IsPlayable(slot) && !this.plan.Roles.ContainsKey(slot);

    /// <summary>Any column, likelier the farther it is from the start's, so the push has a flank to it.</summary>
    private int PickExitColumn(int startColumn)
    {
        var weights = new List<float>();
        for (int x = 1; x < this.width - 1; x++)
        {
            weights.Add(1f + Math.Abs(x - startColumn));
        }

        return 1 + Math.Max(0, this.rng.PickWeighted(weights));
    }

    private void Dig(Band band, Int3 slot, Int3? from)
    {
        this.plan.Occupy(slot, SlotRole.Branch);
        this.plan.RequireTag(slot, band.Trench);
        this.network.Add(slot);
        if (from is { } previous)
        {
            this.JoinTrench(previous, slot);
        }
    }

    private void JoinTrench(Int3 a, Int3 b)
    {
        this.plan.Link(a, b);
        this.trenches.Add(new SlotEdge(a, b));
    }

    /// <summary>
    /// Pins every face of the trench network: a trench socket where the
    /// trench runs on, ground everywhere else. Tags alone would let the
    /// solver turn trench cells so that two of them meet ground to ground,
    /// which breaks the line into pieces.
    /// </summary>
    private void RequireTrenchSockets()
    {
        foreach (Int3 slot in this.network)
        {
            for (int f = 0; f < 4; f++)
            {
                var face = (CellFace)f;
                bool trench = this.trenches.Contains(new SlotEdge(slot, slot + SlotMath.Offset(face)));
                this.plan.RequiredSockets[(slot, face)] = trench ? TrenchSocket : GroundSocket;
            }
        }
    }

    // ------------------------- trenches -------------------------

    /// <summary>
    /// The front line: west to east across the band's two forward rows,
    /// stepping between them now and then but never twice running, so each
    /// column is visited once per row at most and the line never touches
    /// itself.
    /// </summary>
    private void CarveLine(Band band)
    {
        int row = band.Front + (this.rng.Chance(0.5f) ? 0 : band.Rearward);
        var at = new Int3(1, this.layer, row);
        this.Dig(band, at, null);
        band.Line.Add(at);
        bool stepped = false;
        while (true)
        {
            Int3 next;
            var across = new Int3(at.X, this.layer, (at.Z == band.Front) ? band.Front + band.Rearward : band.Front);
            if (!stepped && this.rng.Chance(this.def.LineBendChance) && this.IsFree(across))
            {
                next = across;
                stepped = true;
            }
            else if (at.X < this.width - 2)
            {
                next = at + new Int3(1, 0, 0);
                stepped = false;
            }
            else
            {
                break;
            }

            this.Dig(band, next, at);
            band.Line.Add(next);
            at = next;
        }
    }

    /// <summary>Digs from <paramref name="from"/> toward no man's land until it meets the front line.</summary>
    /// <returns>The trench, from <paramref name="from"/> to the line slot it joins, both included.</returns>
    private List<Int3> Communication(Band band, Int3 from)
    {
        var trench = new List<Int3> { from };
        var forward = new Int3(0, 0, -band.Rearward);
        Int3 at = from;
        while (true)
        {
            Int3 next = at + forward;
            trench.Add(next);
            if (band.Line.Contains(next))
            {
                this.JoinTrench(at, next);
                return trench;
            }

            this.Dig(band, next, at);
            at = next;
        }
    }

    /// <summary>Dead-end communication trenches from the back row, kept a column apart from each other.</summary>
    private void ExtraCommunication(Band band)
    {
        for (int i = 0; i < this.def.CommunicationTrenches; i++)
        {
            var roots = new List<Int3>();
            for (int x = 1; x < this.width - 1; x++)
            {
                var root = new Int3(x, this.layer, band.Back);
                if (this.IsFree(root) && band.Columns.TrueForAll(c => Math.Abs(c - x) >= 2))
                {
                    roots.Add(root);
                }
            }

            if (roots.Count == 0)
            {
                return;
            }

            Int3 chosen = roots[this.rng.Next(roots.Count)];
            band.Columns.Add(chosen.X);
            this.Dig(band, chosen, null);
            this.Communication(band, chosen);
        }
    }

    /// <summary>One-slot saps out of the front row into no man's land, never side by side.</summary>
    private void DigSaps(Band band)
    {
        var forward = new Int3(0, 0, -band.Rearward);
        int lastSap = int.MinValue;
        foreach (Int3 slot in band.Line)
        {
            Int3 sap = slot + forward;
            if (slot.Z == band.Front && slot.X - lastSap >= 2 && this.IsFree(sap) && this.rng.Chance(this.def.SapChance))
            {
                this.Dig(band, sap, slot);
                lastSap = slot.X;
            }
        }
    }

    // ------------------------- structures -------------------------

    /// <summary>Places the recipe's structures in the band's rear rows, clear of its communication trenches.</summary>
    private bool PlaceStructures(Band band, out string failure)
    {
        for (int i = 0; i < this.def.Structures.Count; i++)
        {
            if (this.def.Structures[i] is not { } structure)
            {
                continue;
            }

            int wanted = MacroPlanner.StructureCount(structure, this.rng, this.context);
            int placed = 0;
            while (placed < wanted && this.PlaceStructure(structure, $"structures[{i}]", band))
            {
                placed++;
            }

            if (structure.Requirement == StructureRequirement.Required && placed < Math.Max(1, structure.MinCount))
            {
                failure = $"structures[{i}] ('{structure.Id}'): no room in the rear rows with an entrance onto open ground";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private bool PlaceStructure(StructureData def, string path, Band band)
    {
        var area = new StructurePlacer.Area(
            this.layer,
            this.width,
            this.depth,
            this.IsPlayable,
            slot => this.IsFree(slot) && band.IsRear(slot.Z) && !band.Columns.Contains(slot.X));
        return StructurePlacer.Place(def, path, this.plan, area, this.rng, this.report);
    }

    // ------------------------- the rest -------------------------

    /// <summary>Everything not yet planned: the border, each band's open ground, and no man's land.</summary>
    private void Fill(Band friendly, Band enemy)
    {
        for (int z = 0; z < this.depth; z++)
        {
            for (int x = 0; x < this.width; x++)
            {
                var slot = new Int3(x, this.layer, z);
                if (this.plan.Roles.ContainsKey(slot))
                {
                    continue;
                }

                this.plan.Occupy(slot, SlotRole.Branch);
                this.plan.RequireTag(slot, this.GroundTag(slot, friendly, enemy));
            }
        }
    }

    private string GroundTag(Int3 slot, Band friendly, Band enemy)
    {
        if (!this.IsPlayable(slot))
        {
            return BorderTag;
        }

        if (friendly.Contains(slot.Z))
        {
            return friendly.Ground;
        }

        return enemy.Contains(slot.Z) ? enemy.Ground : NoMansLandTag;
    }

    /// <summary>Start, up its communication trench, straight over the top, along the enemy line, down to the exit.</summary>
    private void CriticalPath(List<Int3> fromStart, List<Int3> fromExit, Band enemy)
    {
        var path = new List<Int3>(fromStart);
        Int3 at = path[^1];
        while (!enemy.Line.Contains(at))
        {
            at += new Int3(0, 0, -1);
            path.Add(at);
        }

        int from = enemy.Line.IndexOf(at);
        int to = enemy.Line.IndexOf(fromExit[^1]);
        int step = Math.Sign(to - from);
        for (int i = from; i != to; i += step)
        {
            path.Add(enemy.Line[i + step]);
        }

        for (int i = fromExit.Count - 2; i >= 0; i--)
        {
            path.Add(fromExit[i]);
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                this.plan.Link(path[i - 1], path[i]);
            }

            if (this.plan.Roles[path[i]] == SlotRole.Branch)
            {
                this.plan.Occupy(path[i], SlotRole.Path);
            }

            this.plan.CriticalPath.Add(path[i]);
        }
    }

    /// <summary>One side's rows: <c>Front</c> faces no man's land, <c>Rearward</c> steps away from it.</summary>
    private sealed class Band(string trench, string ground, int front, int rearward, int rows)
    {
        public string Trench { get; } = trench;

        public string Ground { get; } = ground;

        public int Front { get; } = front;

        public int Rearward { get; } = rearward;

        public int Back => this.Front + (this.Rearward * (rows - 1));

        /// <summary>Gets the front line, west to east.</summary>
        public List<Int3> Line { get; } = [];

        /// <summary>Gets the columns communication trenches already use.</summary>
        public List<int> Columns { get; } = [];

        public bool Contains(int z) => this.Offset(z) >= 0 && this.Offset(z) < rows;

        public bool IsRear(int z) => this.Offset(z) >= LineRows && this.Offset(z) < rows;

        private int Offset(int z) => (z - this.Front) * this.Rearward;
    }
}
