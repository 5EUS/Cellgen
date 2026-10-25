namespace Cellgen.Generation;

/// <summary>
/// <para>
/// Stage C: spends the level's budgets. Each spawn zone gets an encounter
/// that fits its own rules and its fair share of what is left; each item
/// spawn rolls from its table.
/// </para>
/// <para>
/// Required zones are filled first and optional ones get the remainder, so
/// a tight budget thins out the side rooms rather than the critical path.
/// </para>
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="Populator"/> class.</remarks>
/// <param name="def">The recipe.</param>
/// <param name="context">Depth and encounters.</param>
/// <param name="seed">The attempt's seed; zone seeds derive from it.</param>
/// <param name="rng">This stage's random stream.</param>
/// <param name="report">Collects diagnostics.</param>
internal sealed class Populator(LevelData def, GenContext context, ulong seed, GenRng rng, GenReport report)
{
    private readonly LevelData def = def;
    private readonly GenContext context = context;
    private readonly GenRng rng = rng;
    private readonly GenReport report = report;
    private readonly ulong seed = seed;
    private IReadOnlyList<PlannedCell> cells = [];

    /// <summary>Gets the zones given encounters, by handle.</summary>
    public List<PlannedZone> Zones { get; } = [];

    /// <summary>Gets the items rolled.</summary>
    public List<PlannedItem> Items { get; } = [];

    /// <summary>Gets the props placed.</summary>
    public List<PlannedProp> Props { get; } = [];

    /// <summary>Gets how many zones got nothing.</summary>
    public int EmptyZones { get; private set; }

    /// <summary>Gets the encounter budget after depth scaling.</summary>
    public float EncounterBudget { get; private set; }

    /// <summary>Gets what the zones spent.</summary>
    public float EncounterSpent { get; private set; }

    /// <summary>Gets the loot budget after depth scaling.</summary>
    public float LootBudget { get; private set; }

    /// <summary>Gets what the pickups spent.</summary>
    public float LootSpent { get; private set; }

    /// <summary>Fills the cells' zones and item spawns.</summary>
    /// <param name="cells">The level's cells.</param>
    public void Populate(IReadOnlyList<PlannedCell> cells)
    {
        this.cells = cells;
        float growth = MathF.Pow(Math.Max(1f, this.context.Encounters.BudgetGrowth), this.context.Depth);
        this.EncounterBudget = this.def.EncounterBudget * growth * PolicyEval.EncounterBudgetScale(this.def, this.context);
        this.LootBudget = this.def.LootBudget * growth * PolicyEval.LootBudgetScale(this.def, this.context);

        var required = new List<(int Cell, ZoneData Zone)>();
        var optional = new List<(int Cell, ZoneData Zone)>();
        foreach (PlannedCell cell in cells)
        {
            foreach (ZoneData zone in cell.Info.Zones.Where(static z => z is not null))
            {
                (zone.Optional ? optional : required).Add((cell.Index, zone));
            }
        }

        float remaining = this.EncounterBudget;
        remaining = this.FillZones(required, remaining, isOptional: false);
        remaining = this.FillZones(optional, remaining, isOptional: true);
        this.EncounterSpent = this.EncounterBudget - remaining;

        this.RollItems(cells);
        this.RollProps(cells);
    }

    /// <summary>
    /// Places up to <see cref="LevelData.PropMax"/> pieces on prop spawns. Its own random
    /// stream, so enabling it never shifts the encounters and loot a seed already produced.
    /// </summary>
    private void RollProps(IReadOnlyList<PlannedCell> cells)
    {
        if (this.def.PropMax <= 0)
        {
            return;
        }

        var spawns = new List<(int Cell, PropSpawnData Spawn)>();
        foreach (PlannedCell cell in cells.Where(c => !this.def.PropExcludedTags.Any(c.Info.HasTag)))
        {
            spawns.AddRange(cell.Info.PropSpawns
                .Where(s => s is { PropIds.Count: > 0 } && (s.RequiredTags.Count == 0 || s.RequiredTags.Any(cell.Info.HasTag)))
                .Select(s => (cell.Index, s)));
        }

        // The stage label predates the rename and seeds the stream: changing it reshuffles every level.
        GenRng propRng = GenRng.ForStage(this.seed, "artillery");
        propRng.Shuffle(spawns);
        foreach ((int cell, PropSpawnData spawn) in spawns)
        {
            if (this.Props.Count >= this.def.PropMax)
            {
                break;
            }

            if (propRng.Chance(spawn.Chance))
            {
                IReadOnlyList<string> ids = this.AllowedProps(cell, spawn);
                if (ids.Count == 0)
                {
                    continue;
                }

                string propId = ids[propRng.Next(ids.Count)];
                this.Props.Add(new PlannedProp(this.Props.Count, cell, spawn, propId));
            }
        }
    }

    private IReadOnlyList<string> AllowedProps(int cell, PropSpawnData spawn)
    {
        if (!this.context.HasPolicies)
        {
            return spawn.PropIds;
        }

        PlannedCell planned = this.cells[cell];
        return [.. spawn.PropIds.Where(id => PolicyEval.AllowProp(this.context, new PropQuery(this.def, this.context, planned, spawn, id, this.Props.Count)))];
    }

    private float FillZones(List<(int Cell, ZoneData Zone)> zones, float remaining, bool isOptional)
    {
        List<(int Cell, ZoneData Zone)> ordered = this.rng.WeightedOrderList(zones, static z => Math.Max(0.01f, z.Zone.Weight));
        float weightLeft = ordered.Sum(static z => Math.Max(0.01f, z.Zone.Weight));

        foreach ((int cell, ZoneData zone) in ordered)
        {
            float weight = Math.Max(0.01f, zone.Weight);
            float fair = remaining * weight / Math.Max(weightLeft, 0.01f);
            weightLeft -= weight;

            var fits = new List<EncounterEntry>();
            var policyWeights = new List<float>();
            foreach (EncounterEntry e in this.context.Encounters.Entries)
            {
                if (!this.Accepts(zone, e) || e.Cost > remaining + 1e-4f)
                {
                    continue;
                }

                float policy = this.context.HasPolicies
                    ? PolicyEval.EncounterWeight(this.context, new EncounterQuery(this.def, this.context, this.cells[cell], zone, e, remaining, fair))
                    : 1f;
                if (policy > 0f)
                {
                    fits.Add(e);
                    policyWeights.Add(policy);
                }
            }

            if (fits.Count == 0 || zone.SpawnPoints.Count == 0)
            {
                this.EmptyZones++;
                if (!isOptional && this.context.Encounters.Entries.Count > 0)
                {
                    this.report.Warn($"cells[{cell}] {zone.NodePath}", $"no encounter fits (budget left {remaining:0.##}); left empty");
                }

                continue;
            }

            // Closest to the zone's fair share is likeliest, so big budgets buy
            // big encounters rather than many small ones everywhere.
            int pick = this.rng.PickWeighted(fits.Select((e, i) => (1f / (1f + MathF.Abs(e.Cost - fair))) * policyWeights[i]).ToList());
            EncounterEntry chosen = fits[pick];
            int handle = this.Zones.Count;
            this.Zones.Add(new PlannedZone(handle, cell, zone, chosen.Id, chosen.Cost, StableHash.Derive(this.seed, $"zone:{handle}")));
            remaining -= chosen.Cost;
        }

        return remaining;
    }

    private bool Accepts(ZoneData zone, EncounterEntry encounter)
    {
        if (!encounter.AllowsDepth(this.context.Depth) || !encounter.HasAnyTag(this.def.EncounterTags))
        {
            return false;
        }

        bool listed = (zone.EncounterIds.Count > 0)
            ? zone.EncounterIds.Contains(encounter.Id, StringComparer.Ordinal)
            : encounter.HasAnyTag(zone.EncounterTags);
        return listed && encounter.Cost >= zone.MinCost && (zone.MaxCost <= 0f || encounter.Cost <= zone.MaxCost);
    }

    private void RollItems(IReadOnlyList<PlannedCell> cells)
    {
        var spawns = new List<(int Cell, ItemSpawnData Spawn)>();
        foreach (PlannedCell cell in cells)
        {
            spawns.AddRange(cell.Info.Items.Where(static s => s is not null).Select(s => (cell.Index, s!)));
        }

        this.rng.Shuffle(spawns);
        float remaining = this.LootBudget;
        foreach ((int cell, ItemSpawnData spawn) in spawns)
        {
            LootTableData? table = spawn.LootTable ?? this.def.LootTable;
            if (table is null)
            {
                continue;
            }

            if (spawn.Mode == ItemSpawnMode.Shop)
            {
                this.StockShop(cell, spawn, table);
            }
            else if (spawn.Mode == ItemSpawnMode.Ammo)
            {
                if (this.rng.Chance(spawn.Chance))
                {
                    this.RollAmmo(cell, spawn, table);
                }
            }
            else if (this.rng.Chance(spawn.Chance))
            {
                remaining = this.RollLoot(cell, spawn, table, remaining);
            }
        }

        this.LootSpent = this.LootBudget - remaining;
    }

    private float RollLoot(int cell, ItemSpawnData spawn, LootTableData table, float remaining)
    {
        for (int i = 0; i < Math.Max(1, spawn.Count); i++)
        {
            List<LootEntryData> fits = this.Eligible(table, spawn).Where(e => e.Cost <= remaining + 1e-4f && this.Offers(cell, spawn, e, remaining)).ToList();
            if (fits.Count == 0)
            {
                break;
            }

            LootEntryData entry = fits[Math.Max(0, this.rng.PickWeighted(fits.Select(e => this.WeightOf(cell, spawn, e, remaining)).ToList()))];
            this.Items.Add(new PlannedItem(cell, spawn, entry.ItemId, 0f));
            remaining -= entry.Cost;
        }

        return remaining;
    }

    /// <summary>Free pickups that cost no loot budget: the table's weights alone decide.</summary>
    private void RollAmmo(int cell, ItemSpawnData spawn, LootTableData table)
    {
        List<LootEntryData> pool = this.Eligible(table, spawn).Where(e => this.Offers(cell, spawn, e, 0f)).ToList();
        for (int i = 0; i < Math.Max(1, spawn.Count) && pool.Count > 0; i++)
        {
            LootEntryData entry = pool[Math.Max(0, this.rng.PickWeighted(pool.Select(e => this.WeightOf(cell, spawn, e, 0f)).ToList()))];
            this.Items.Add(new PlannedItem(cell, spawn, entry.ItemId, 0f));
        }
    }

    /// <summary>A shop's offers: distinct entries where possible, priced, outside the loot budget.</summary>
    private void StockShop(int cell, ItemSpawnData spawn, LootTableData table)
    {
        List<LootEntryData> stock = this.Eligible(table, spawn).Where(e => this.Offers(cell, spawn, e, 0f)).ToList();
        for (int i = 0; i < Math.Max(1, spawn.Count) && stock.Count > 0; i++)
        {
            int pick = Math.Max(0, this.rng.PickWeighted(stock.Select(e => this.WeightOf(cell, spawn, e, 0f)).ToList()));
            this.Items.Add(new PlannedItem(cell, spawn, stock[pick].ItemId, stock[pick].Cost));
            stock.RemoveAt(pick);
        }
    }

    private bool Offers(int cell, ItemSpawnData spawn, LootEntryData entry, float budgetLeft) =>
        !this.context.HasPolicies || PolicyEval.LootWeight(this.context, new LootQuery(this.def, this.context, this.cells[cell], spawn, entry, budgetLeft)) > 0f;

    private float WeightOf(int cell, ItemSpawnData spawn, LootEntryData entry, float budgetLeft) =>
        this.context.HasPolicies
            ? entry.Weight * PolicyEval.LootWeight(this.context, new LootQuery(this.def, this.context, this.cells[cell], spawn, entry, budgetLeft))
            : entry.Weight;

    private IEnumerable<LootEntryData> Eligible(LootTableData table, ItemSpawnData spawn)
    {
        int depth = this.context.Depth;
        return table.Resolve(this.context.Loot).Where(e =>
            !string.IsNullOrEmpty(e.ItemId)
            && e.Weight > 0f
            && depth >= e.MinDepth && (e.MaxDepth < 0 || depth <= e.MaxDepth)
            && (spawn.Tags.Count == 0 || spawn.Tags.Any(t => e.Tags.Contains(t, StringComparer.Ordinal))));
    }
}
