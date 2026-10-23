using System.Globalization;

namespace Cellgen.Generation;

/// <summary>
/// What a recipe does across many seeds: how often it fails, how hard the
/// generator has to work, and what the levels look like on average. The
/// preview dock's batch run and <c>tools/procgen_check.gd</c> both use it.
/// </summary>
public sealed class LevelStats
{
    /// <summary>How many seeds are regenerated to check determinism.</summary>
    private const int DeterminismSamples = 8;

    private readonly List<ulong> failedSeeds = [];
    private readonly List<string> failures = [];
    private readonly Dictionary<string, int> cellUse = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> structureUse = new(StringComparer.Ordinal);

    /// <summary>Gets the seeds run.</summary>
    public int Seeds { get; private set; }

    /// <summary>Gets the seeds that produced no level at all.</summary>
    public int Failed { get; private set; }

    /// <summary>Gets the seeds that needed the recipe's fallback.</summary>
    public int UsedFallback { get; private set; }

    /// <summary>Gets the total attempts across every seed.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the total cells across every level.</summary>
    public int Cells { get; private set; }

    /// <summary>Gets the total armed zones across every level.</summary>
    public int Zones { get; private set; }

    /// <summary>Gets the total empty zones across every level.</summary>
    public int EmptyZones { get; private set; }

    /// <summary>Gets the total props placed across every level.</summary>
    public int Props { get; private set; }

    /// <summary>Gets the seeds that failed, for reproducing them.</summary>
    public IReadOnlyList<ulong> FailedSeeds => this.failedSeeds;

    /// <summary>Gets problems that should fail a CI run.</summary>
    public IReadOnlyList<string> Failures => this.failures;

    /// <summary>Gets how many levels each cell appeared in, by scene file name.</summary>
    public IReadOnlyDictionary<string, int> CellUse => this.cellUse;

    /// <summary>Gets how many levels each structure appeared in.</summary>
    public IReadOnlyDictionary<string, int> StructureUse => this.structureUse;

    /// <summary>Generates seeds <c>1..count</c> at the recipe's lowest depth.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="catalog">The encounters.</param>
    /// <param name="count">How many seeds.</param>
    /// <param name="depth">The depth; null for the recipe's lowest.</param>
    /// <param name="unlocks">Unlock flags, for unlockable structures.</param>
    /// <param name="loot">Where loot tables find their pools; null for none.</param>
    /// <returns>The statistics.</returns>
    public static LevelStats Sweep(LevelData def, EncounterCatalog catalog, int count, int? depth = null, IReadOnlySet<string>? unlocks = null, ILootCatalog? loot = null)
    {
        var stats = new LevelStats();
        for (int i = 1; i <= count; i++)
        {
            var context = new GenContext
            {
                Seed = (ulong)i,
                Depth = depth ?? Math.Max(0, def.MinDepth),
                Encounters = catalog,
                UnlockFlags = unlocks ?? new HashSet<string>(StringComparer.Ordinal),
                Loot = loot ?? ILootCatalog.Empty,
            };
            GenResult result = LevelGenerator.Generate(def, context);
            stats.Add(context.Seed, result);

            if (i <= DeterminismSamples && result.Plan is { } plan)
            {
                string again = LevelGenerator.Generate(def, context).Plan?.Fingerprint() ?? "none";
                if (!string.Equals(again, plan.Fingerprint(), StringComparison.Ordinal))
                {
                    stats.failures.Add($"seed {i} is not deterministic: {plan.Fingerprint()} then {again}");
                }
            }
        }

        return stats;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        float ok = Math.Max(1, this.Seeds - this.Failed);
        float attempts = (float)this.Attempts / Math.Max(1, this.Seeds);
        string runs = string.Create(CultureInfo.InvariantCulture, $"{this.Seeds} seeds, {this.Failed} failed, {this.UsedFallback} fell back, {attempts:0.0} attempts/seed");
        string shape = string.Create(CultureInfo.InvariantCulture, $"{this.Cells / ok:0.0} cells, {this.Zones / ok:0.0} zones ({this.EmptyZones / ok:0.0} empty) per level");
        string props = this.Props > 0 ? string.Create(CultureInfo.InvariantCulture, $", {this.Props / ok:0.0} props") : string.Empty;
        return $"{runs}, {shape}{props}";
    }

    private void Add(ulong seed, GenResult result)
    {
        this.Seeds++;
        this.Attempts += result.Report.Attempts;
        if (result.Report.UsedFallback)
        {
            this.UsedFallback++;
        }

        if (result.Plan is not { } plan)
        {
            this.Failed++;
            this.failedSeeds.Add(seed);
            this.failures.Add($"seed {seed} produced no level: {string.Join("; ", result.Report.Lines())}");
            return;
        }

        this.CheckProps(seed, plan);
        this.Cells += plan.Cells.Count;
        this.Zones += plan.Zones.Count;
        this.EmptyZones += plan.EmptyZones;
        var structures = new HashSet<string>(StringComparer.Ordinal);
        foreach (PlannedCell cell in plan.Cells)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(cell.Info.ScenePath);
            this.cellUse[name] = this.cellUse.GetValueOrDefault(name) + 1;
            if (cell.StructureId.Length > 0)
            {
                structures.Add(cell.StructureId);
            }
        }

        foreach (string id in structures)
        {
            this.structureUse[id] = this.structureUse.GetValueOrDefault(id) + 1;
        }
    }

    /// <summary>Props go only in cells that allow them, and never past the recipe's limit.</summary>
    private void CheckProps(ulong seed, LevelPlan plan)
    {
        this.Props += plan.Props.Count;
        if (plan.Props.Count > plan.Def.PropMax)
        {
            this.failures.Add($"seed {seed} placed {plan.Props.Count} props, over the recipe's {plan.Def.PropMax}");
        }

        foreach (PlannedProp prop in plan.Props)
        {
            CellData cell = plan.Cells[prop.CellIndex].Info;
            if (plan.Def.PropExcludedTags.Any(cell.HasTag))
            {
                this.failures.Add($"seed {seed}: prop '{prop.PropId}' placed in {cell.ScenePath}, which excludes props");
            }
        }
    }
}
