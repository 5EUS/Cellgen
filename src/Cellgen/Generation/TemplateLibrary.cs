namespace Cellgen.Generation;

/// <summary>
/// Everything one recipe can place, checked once per generation: the cells of
/// its sets as templates, its structures' pieces, and socket compatibility.
/// </summary>
internal sealed class TemplateLibrary
{
    private readonly Dictionary<(string, string), bool> compatible = [];

    private readonly HashSet<int> downTurns = [];
    private readonly HashSet<int> upTurns = [];

    private TemplateLibrary(List<CellTemplate> cells)
    {
        this.Cells = cells;
        bool hasDown = false;
        bool hasUp = false;
        foreach (CellTemplate cell in cells)
        {
            foreach (CellVariant variant in cell.Variants)
            {
                this.AddShaftTurns(variant);
                hasDown |= variant.HasSocketOn(CellFace.Down);
                hasUp |= variant.HasSocketOn(CellFace.Up);
            }
        }

        // Separate cells joined by Up/Down sockets can be combined freely, so
        // any turn is possible once both halves exist.
        if (hasDown && hasUp)
        {
            this.downTurns.UnionWith([0, 1, 2, 3]);
            this.upTurns.UnionWith([0, 1, 2, 3]);
        }
    }

    /// <summary>Gets the cells the solver may place.</summary>
    public IReadOnlyList<CellTemplate> Cells { get; }

    /// <summary>Gets a value indicating whether any cell connects two layers, so the path may change layer.</summary>
    public bool CanChangeLayer => this.downTurns.Count > 0 || this.upTurns.Count > 0;

    /// <summary>
    /// Builds the library for a recipe, reporting cells that can't be used
    /// and stale bakes. Cells at depths the run is not at are left out.
    /// </summary>
    /// <param name="def">The recipe.</param>
    /// <param name="depth">The run depth.</param>
    /// <param name="report">Collects diagnostics.</param>
    /// <returns>The library.</returns>
    public static TemplateLibrary Build(LevelData def, int depth, GenReport report)
    {
        var cells = new List<CellTemplate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int s = 0; s < def.CellSets.Count; s++)
        {
            CellSetData? set = def.CellSets[s];
            if (set is null)
            {
                report.Warn($"cell_sets[{s}]", "empty slot");
                continue;
            }

            for (int c = 0; c < set.Cells.Count; c++)
            {
                if (Template(set.Cells[c], set.Weight, $"cell_sets[{s}].cells[{c}]", report) is not { } template)
                {
                    continue;
                }

                if (seen.Add(template.Info.ScenePath) && template.AllowsDepth(depth))
                {
                    cells.Add(template);
                }
            }
        }

        if (cells.Count == 0)
        {
            report.Error("cell_sets", "no usable cells; bake the sets' cells (Rescan & Bake on each CellSetData)");
        }

        return new TemplateLibrary(cells);
    }

    /// <summary>A template for one baked cell, or null (with a diagnostic) if it can't be used.</summary>
    /// <param name="info">The baked cell.</param>
    /// <param name="weightScale">A multiplier on its weight.</param>
    /// <param name="path">Where it came from, for diagnostics.</param>
    /// <param name="report">Collects diagnostics.</param>
    /// <param name="rotationMask">Overrides the cell's allowed rotations; structures fix their pieces' turns.</param>
    /// <returns>The template.</returns>
    public static CellTemplate? Template(CellData? info, float weightScale, string path, GenReport report, int? rotationMask = null)
    {
        if (info is null)
        {
            report.Warn(path, "empty slot");
            return null;
        }

        if (info.Footprint.X < 1 || info.Footprint.Y < 1 || info.Footprint.Z < 1)
        {
            report.Error(path, $"{info.ScenePath} has footprint {info.Footprint}; every axis must be at least 1");
            return null;
        }

        if (info.IsStale)
        {
            report.Warn(path, $"{info.ScenePath} changed since it was baked; using the old bake");
        }

        int mask = rotationMask ?? (info.AllowedRotations & 0b1111);
        if (mask == 0)
        {
            report.Warn(path, $"{info.ScenePath} allows no rotations; never placed");
            return null;
        }

        return new CellTemplate(info, Math.Max(0f, info.Weight * weightScale), mask);
    }

    /// <summary>
    /// The quarter turns (0 straight on, 1 right, 2 back, 3 left) a path can
    /// make across a change of layer, given the cells available: which
    /// horizontal heading it may leave the lower (or upper) slot in, relative
    /// to the one it arrived with.
    /// </summary>
    /// <param name="face">Down or Up.</param>
    /// <returns>The allowed turns; empty when the layer can't be changed that way.</returns>
    public IReadOnlySet<int> TurnsAcross(CellFace face) => (face == CellFace.Down) ? this.downTurns : this.upTurns;

    /// <summary>Gets whether two sockets may face each other. Cached by id pair.</summary>
    /// <param name="a">One socket.</param>
    /// <param name="b">The other.</param>
    /// <returns>True when compatible.</returns>
    public bool Compatible(SocketData a, SocketData b)
    {
        (string, string) key = (a.Id.ToString(), b.Id.ToString());
        if (!this.compatible.TryGetValue(key, out bool ok))
        {
            ok = a.Accepts(b);
            this.compatible[key] = ok;
        }

        return ok;
    }

    /// <summary>
    /// The turns a single-column, two-layer cell (a shaft) allows: arriving
    /// through a socket on one layer and leaving through one on the other.
    /// </summary>
    private void AddShaftTurns(CellVariant variant)
    {
        if (variant.Footprint != new Int3(1, 2, 1))
        {
            return;
        }

        var bottom = Int3.Zero;
        var top = new Int3(0, 1, 0);
        for (int a = 0; a < 4; a++)
        {
            for (int b = 0; b < 4; b++)
            {
                if (variant.SocketAt(top, (CellFace)a) is null || variant.SocketAt(bottom, (CellFace)b) is null)
                {
                    continue;
                }

                // Down: in through the top's face a (heading away from it),
                // out through the bottom's face b. Up is the reverse.
                this.downTurns.Add(SlotMath.Mod(b - (int)SlotMath.Opposite((CellFace)a), 4));
                this.upTurns.Add(SlotMath.Mod(a - (int)SlotMath.Opposite((CellFace)b), 4));
            }
        }
    }
}
