namespace Cellgen.Generation;

/// <summary>
/// Stage B: chooses a cell, rotation and anchor for every slot the macro
/// plan occupied, so that every required link is an open, compatible pair of
/// sockets and no open socket faces something it can't.
///
/// A depth-first search that always fills the most constrained slot next
/// (fewest fitting placements), tries placements in weighted-random order,
/// and backtracks on a dead end - within a step budget, past which the
/// attempt fails and the generator retries on another seed. Forward checking
/// is what keeps it fast: a slot with no placement left is found the step it
/// happens, not twenty steps later.
/// </summary>
internal sealed class CellSolver
{
    private readonly LevelData def;
    private readonly GenContext context;
    private readonly TemplateLibrary library;
    private readonly MacroPlan plan;
    private readonly GenRng rng;
    private readonly int budget;
    private readonly Dictionary<Int3, PlacedPiece> assigned = [];
    private readonly Dictionary<CellTemplate, int> uses = [];
    private readonly List<Int3> open = [];
    private readonly List<PlacedPiece> @fixed = [];
    private readonly Dictionary<Int3, List<PlacedPiece>> candidates = [];
    private readonly int reach;
    private int steps;

    /// <summary>Initializes a new instance of the <see cref="CellSolver"/> class.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">The seed, depth and policies.</param>
    /// <param name="library">What can be placed.</param>
    /// <param name="plan">The topology to realize.</param>
    /// <param name="rng">This stage's random stream.</param>
    public CellSolver(LevelData def, GenContext context, TemplateLibrary library, MacroPlan plan, GenRng rng)
    {
        this.def = def;
        this.context = context;
        this.library = library;
        this.plan = plan;
        this.rng = rng;

        foreach (StructurePlacement structure in plan.Structures)
        {
            this.@fixed.AddRange(structure.Pieces);
        }

        // Path order, then branches in the order they grew: a deterministic
        // order that also puts the most connected slots first.
        foreach (Int3 slot in plan.CriticalPath)
        {
            this.open.Add(slot);
        }

        foreach ((Int3 slot, SlotRole role) in plan.Roles)
        {
            if (role == SlotRole.Branch)
            {
                this.open.Add(slot);
            }
        }

        this.budget = 64 * this.open.Count;

        // A placement can change what fits any slot a piece covering it could
        // reach: the piece's own extent, plus the neighbours its faces check.
        foreach (Int3 footprint in library.Cells.SelectMany(static template => template.Variants).Select(static variant => variant.Footprint))
        {
            this.reach = System.Math.Max(this.reach, System.Math.Max(footprint.X, System.Math.Max(footprint.Y, footprint.Z)));
        }
    }

    /// <summary>Gets how many placements the search tried.</summary>
    public int Steps => this.steps;

    /// <summary>Solves the plan.</summary>
    /// <param name="failure">Why it failed, when it did.</param>
    /// <returns>Every placed piece, structures included, sorted by anchor; or null.</returns>
    public List<PlacedPiece>? Solve(out string failure)
    {
        foreach (PlacedPiece piece in this.@fixed)
        {
            this.Assign(piece);
        }

        var stack = new Stack<Frame>();
        while (true)
        {
            if (this.steps > this.budget)
            {
                failure = $"cell search gave up after {this.steps} placements";
                return null;
            }

            if (!this.MostConstrained(out List<PlacedPiece> viable, out bool done))
            {
                if (!this.Backtrack(stack))
                {
                    failure = this.DescribeDeadEnd();
                    return null;
                }

                continue;
            }

            if (done)
            {
                break;
            }

            List<PlacedPiece> ordered = this.rng.WeightedOrderList(viable, this.WeightOf);
            var frame = new Frame(ordered);
            this.Assign(frame.Take());
            stack.Push(frame);
            this.steps++;
        }

        if (!this.FixedPiecesFit(out failure))
        {
            return null;
        }

        var pieces = new List<PlacedPiece>(new HashSet<PlacedPiece>(this.assigned.Values));
        pieces.Sort(static (a, b) => Compare(a.Anchor, b.Anchor));
        failure = string.Empty;
        return pieces;
    }

    private static int Compare(Int3 a, Int3 b)
    {
        int c = a.Y.CompareTo(b.Y);
        if (c == 0)
        {
            c = a.Z.CompareTo(b.Z);
        }

        return (c == 0) ? a.X.CompareTo(b.X) : c;
    }

    /// <summary>Whether two sockets facing each other across a face are acceptable.</summary>
    private bool Pairs(SocketData? a, SocketData? b, bool linked)
    {
        if (a is null && b is null)
        {
            return !linked;
        }

        if (a is not null && b is not null)
        {
            return this.library.Compatible(a, b);
        }

        // One side open, the other closed: never on a required link, and
        // otherwise only for sockets that read fine against a wall.
        return !linked && (a ?? b)!.IsBoundaryOk;
    }

    /// <summary>
    /// Finds the unassigned slot with the fewest placements among those next
    /// to something already placed (or any, at the start).
    /// </summary>
    /// <returns>False when some slot has no placement at all: a dead end.</returns>
    private bool MostConstrained(out List<PlacedPiece> best, out bool done)
    {
        best = [];
        done = true;
        bool any = false;
        bool frontierExists = this.assigned.Count > 0 && this.HasFrontier();
        foreach (Int3 slot in this.open)
        {
            if (this.assigned.ContainsKey(slot) || (frontierExists && !this.TouchesAssigned(slot)))
            {
                continue;
            }

            done = false;
            List<PlacedPiece> slotCandidates = this.CandidatesFor(slot);
            if (slotCandidates.Count == 0)
            {
                return false;
            }

            if (!any || slotCandidates.Count < best.Count)
            {
                best = slotCandidates;
                any = true;
            }
        }

        return true;
    }

    private bool HasFrontier() => this.open.Exists(slot => !this.assigned.ContainsKey(slot) && this.TouchesAssigned(slot));

    private bool TouchesAssigned(Int3 slot) =>
        System.Array.Exists(SlotMath.Faces, face => this.assigned.ContainsKey(slot + SlotMath.Offset(face)));

    /// <summary>
    /// The placements that fit a slot, kept until a placement near it changes
    /// what fits. Recomputing every frontier slot on every step is what makes
    /// a large grid slow; the cache is dropped conservatively around each
    /// assignment, so the answer is always what a fresh search would give.
    /// </summary>
    private List<PlacedPiece> CandidatesFor(Int3 slot)
    {
        if (!this.candidates.TryGetValue(slot, out List<PlacedPiece>? cached))
        {
            cached = this.FindCandidates(slot);
            this.candidates[slot] = cached;
        }

        return cached;
    }

    private void Invalidate(PlacedPiece piece)
    {
        if (piece.Variant.Template.Info.MaxPerLevel > 0)
        {
            this.candidates.Clear();
            return;
        }

        foreach (Int3 covered in piece.Covered())
        {
            for (int x = -this.reach; x <= this.reach; x++)
            {
                for (int y = -this.reach; y <= this.reach; y++)
                {
                    for (int z = -this.reach; z <= this.reach; z++)
                    {
                        this.candidates.Remove(covered + new Int3(x, y, z));
                    }
                }
            }
        }
    }

    private List<PlacedPiece> FindCandidates(Int3 slot)
    {
        var matches = new List<PlacedPiece>();
        foreach (CellTemplate template in this.library.Cells)
        {
            int limit = template.Info.MaxPerLevel;
            if (limit > 0 && this.uses.GetValueOrDefault(template) >= limit)
            {
                continue;
            }

            foreach (CellVariant variant in template.Variants)
            {
                foreach (Int3 local in variant.Slots)
                {
                    var piece = new PlacedPiece(variant, slot - local);
                    if (this.Fits(piece))
                    {
                        matches.Add(piece);
                    }
                }
            }
        }

        return matches;
    }

    private bool Fits(PlacedPiece piece)
    {
        CellTemplate template = piece.Variant.Template;
        int starts = 0;
        int exits = 0;
        foreach (Int3 slot in piece.Covered())
        {
            if (!this.plan.Roles.TryGetValue(slot, out SlotRole role) || role == SlotRole.Structure || this.assigned.ContainsKey(slot))
            {
                return false;
            }

            if (this.plan.RequiredTags.TryGetValue(slot, out string? tag) && !template.HasTag(tag))
            {
                return false;
            }

            starts += (role == SlotRole.Start) ? 1 : 0;
            exits += (role == SlotRole.Exit) ? 1 : 0;
        }

        if ((starts > 0) != template.HasTag(this.def.StartTag) || (exits > 0) != template.HasTag(this.def.ExitTag))
        {
            return false;
        }

        foreach ((Int3 local, CellFace face) in piece.Variant.OuterFaces)
        {
            if (!this.FaceFits(piece, piece.Anchor + local, face))
            {
                return false;
            }
        }

        if (!this.context.HasPolicies)
        {
            return true;
        }

        CellQuery query = this.QueryFor(piece);
        return PolicyEval.AllowCell(this.context, query) && PolicyEval.CellWeight(this.context, query) > 0f;
    }

    private float WeightOf(PlacedPiece piece) =>
        this.context.HasPolicies ? piece.Variant.Weight * PolicyEval.CellWeight(this.context, this.QueryFor(piece)) : piece.Variant.Weight;

    private CellQuery QueryFor(PlacedPiece piece) =>
        new(this.def, this.context, piece.Variant.Template.Info, piece.Anchor, piece.Variant.Rotation);

    private bool FaceFits(PlacedPiece piece, Int3 slot, CellFace face)
    {
        Int3 neighbour = slot + SlotMath.Offset(face);
        SocketData? ours = piece.SocketAt(slot, face);
        if (this.plan.RequiredSockets.TryGetValue((slot, face), out string? required) && !string.Equals(ours?.Id.ToString(), required, System.StringComparison.Ordinal))
        {
            return false;
        }

        bool linked = this.plan.IsLinked(slot, neighbour);

        if (this.assigned.TryGetValue(neighbour, out PlacedPiece? other))
        {
            return this.Pairs(ours, other.SocketAt(neighbour, SlotMath.Opposite(face)), linked);
        }

        if (this.plan.Roles.ContainsKey(neighbour))
        {
            // Filled later; that placement checks this pair. A required link
            // can already be ruled out if this side is closed.
            return !linked || ours is not null;
        }

        return ours is null || ours.IsBoundaryOk;
    }

    /// <summary>Structures were placed before anything around them existed; check they fit what grew there.</summary>
    private bool FixedPiecesFit(out string failure)
    {
        foreach (StructurePlacement structure in this.plan.Structures)
        {
            foreach (PlacedPiece piece in structure.Pieces)
            {
                foreach ((Int3 local, CellFace face) in piece.Variant.OuterFaces)
                {
                    Int3 slot = piece.Anchor + local;
                    Int3 neighbour = slot + SlotMath.Offset(face);
                    if (this.assigned.TryGetValue(neighbour, out PlacedPiece? other) && ReferenceEquals(other, piece))
                    {
                        continue;
                    }

                    SocketData? ours = piece.SocketAt(slot, face);
                    bool ok = this.assigned.TryGetValue(neighbour, out other)
                        ? this.Pairs(ours, other.SocketAt(neighbour, SlotMath.Opposite(face)), this.plan.IsLinked(slot, neighbour))
                        : ours is null || ours.IsBoundaryOk;
                    if (!ok)
                    {
                        failure = $"structure '{structure.Def.Id}' has a socket at {slot} {face} that nothing next to it can meet";
                        return false;
                    }
                }
            }
        }

        failure = string.Empty;
        return true;
    }

    private bool Backtrack(Stack<Frame> stack)
    {
        while (stack.Count > 0)
        {
            Frame top = stack.Peek();
            this.Unassign(top.Applied!);
            if (top.HasMore)
            {
                this.Assign(top.Take());
                this.steps++;
                return true;
            }

            stack.Pop();
        }

        return false;
    }

    private void Assign(PlacedPiece piece)
    {
        foreach (Int3 slot in piece.Covered())
        {
            this.assigned[slot] = piece;
        }

        CellTemplate template = piece.Variant.Template;
        this.uses[template] = this.uses.GetValueOrDefault(template) + 1;
        this.Invalidate(piece);
    }

    private void Unassign(PlacedPiece piece)
    {
        foreach (Int3 slot in piece.Covered())
        {
            this.assigned.Remove(slot);
        }

        CellTemplate template = piece.Variant.Template;
        this.uses[template] = this.uses.GetValueOrDefault(template) - 1;
        this.Invalidate(piece);
    }

    /// <summary>Names the first slot nothing fits, for the report.</summary>
    private string DescribeDeadEnd()
    {
        foreach (Int3 slot in this.open.Where(slot => !this.assigned.ContainsKey(slot)))
        {
            if (this.CandidatesFor(slot).Count == 0)
            {
                string role = this.plan.Roles[slot].ToString().ToUpperInvariant();
                return $"no cell fits the {role} slot {slot} with its required links ({this.LinksOf(slot)})";
            }
        }

        return "the cell search exhausted every option";
    }

    private string LinksOf(Int3 slot)
    {
        var faces = SlotMath.Faces
            .Where(face => this.plan.IsLinked(slot, slot + SlotMath.Offset(face)))
            .Select(static face => face.ToString())
            .ToList();
        return (faces.Count == 0) ? "none" : string.Join('+', faces);
    }

    /// <summary>One decision: the candidates for a slot, and which was tried last.</summary>
    private sealed class Frame(List<PlacedPiece> candidates)
    {
        private int next;

        public PlacedPiece? Applied { get; private set; }

        public bool HasMore => this.next < candidates.Count;

        public PlacedPiece Take()
        {
            this.Applied = candidates[this.next++];
            return this.Applied;
        }
    }
}
