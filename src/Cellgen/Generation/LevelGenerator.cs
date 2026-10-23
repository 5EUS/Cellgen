namespace Cellgen.Generation;

/// <summary>
/// Turns a recipe, a seed and a depth into a <see cref="LevelPlan"/>.
///
/// Each attempt runs the three stages on its own seed - topology
/// (<see cref="MacroPlanner"/>, or <see cref="FrontPlanner"/> or
/// <see cref="WildsPlanner"/> for the whole-grid layouts), cells (<see cref="CellSolver"/>), budgets
/// (<see cref="Populator"/>) - and is validated as a whole: the exit and
/// every structure must be reachable from the start through open, matching
/// sockets. A failed attempt retries on a derived seed; after
/// <c>MaxAttempts</c> the recipe's fallback is used. Nothing here touches the
/// scene tree, so the editor preview, the headless check and the game all
/// run exactly this.
/// </summary>
public static class LevelGenerator
{
    /// <summary>Generates a level.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">Seed, depth, unlocks and encounters.</param>
    /// <returns>The plan and the report.</returns>
    public static GenResult Generate(LevelData def, GenContext context) => GenerateWith(def, context, new GenReport(), allowFallback: true);

    private static GenResult GenerateWith(LevelData def, GenContext context, GenReport report, bool allowFallback)
    {
        TemplateLibrary library = TemplateLibrary.Build(def, context.Depth, report);
        int attempts = Math.Max(1, def.MaxAttempts);
        if (report.ErrorCount == 0)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                report.Attempts++;
                ulong seed = (attempt == 0) ? context.Seed : StableHash.Derive(context.Seed, $"attempt:{attempt}");
                if (TryAttempt(def, context, library, seed, report, out string failure) is { } plan)
                {
                    return new GenResult(plan, report);
                }

                report.AttemptFailed(attempt, failure);
            }
        }

        if (allowFallback && def.Fallback is { } fallback && fallback != def)
        {
            report.Warn("fallback", $"'{def.Id}' produced no level; using fallback '{fallback.Id}'");
            report.UsedFallback = true;
            return GenerateWith(fallback, context, report, allowFallback: false);
        }

        report.Error("level", $"'{def.Id}' produced no valid level in {report.Attempts} attempt(s)");
        return new GenResult(null, report);
    }

    private static LevelPlan? TryAttempt(LevelData def, GenContext context, TemplateLibrary library, ulong seed, GenReport report, out string failure)
    {
        GenRng macroRng = GenRng.ForStage(seed, "macro");
        MacroPlan? macro = PlanMacro(def, context, library, macroRng, report, out failure);

        if (macro is null)
        {
            return null;
        }

        GenRng cellsRng = GenRng.ForStage(seed, "cells");
        List<PlacedPiece>? pieces = new CellSolver(def, library, macro, cellsRng).Solve(out failure);

        if (pieces is null)
        {
            return null;
        }

        List<PlannedCell> cells = ToCells(pieces, macro, out int startCell, out int exitCell);
        if (!Reachable(pieces, library, startCell, exitCell, macro, out failure))
        {
            return null;
        }

        GenRng populateRng = GenRng.ForStage(seed, "populate");
        var populator = new Populator(def, context, seed, populateRng, report);
        populator.Populate(cells);

        return new LevelPlan
        {
            Def = def,
            Seed = context.Seed,
            AttemptSeed = seed,
            Depth = context.Depth,
            Cells = cells,
            CriticalPath = macro.CriticalPath,
            StartCell = startCell,
            ExitCell = exitCell,
            Zones = populator.Zones,
            Items = populator.Items,
            Props = populator.Props,
            EmptyZones = populator.EmptyZones,
            EncounterBudget = populator.EncounterBudget,
            EncounterSpent = populator.EncounterSpent,
            LootBudget = populator.LootBudget,
            LootSpent = populator.LootSpent,
        };
    }

    private static MacroPlan? PlanMacro(LevelData def, GenContext context, TemplateLibrary library, GenRng rng, GenReport report, out string failure) => def.Layout switch
    {
        LevelLayout.Front => new FrontPlanner(def, context, rng, report).Plan(out failure),
        LevelLayout.Wilds => new WildsPlanner(def, context, rng, report).Plan(out failure),
        _ => new MacroPlanner(def, library, context, rng, report).Plan(out failure),
    };

    private static List<PlannedCell> ToCells(List<PlacedPiece> pieces, MacroPlan macro, out int startCell, out int exitCell)
    {
        var structureOf = new Dictionary<PlacedPiece, string>(ReferenceEqualityComparer.Instance);
        foreach (StructurePlacement structure in macro.Structures)
        {
            foreach (PlacedPiece piece in structure.Pieces)
            {
                structureOf[piece] = structure.Def.Id.ToString();
            }
        }

        var cells = new List<PlannedCell>(pieces.Count);
        startCell = -1;
        exitCell = -1;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlacedPiece piece = pieces[i];
            cells.Add(new PlannedCell(i, piece.Variant.Template.Info, piece.Anchor, piece.Variant.Rotation, structureOf.GetValueOrDefault(piece, string.Empty)));
            foreach (Int3 slot in piece.Covered())
            {
                if (slot == macro.Start)
                {
                    startCell = i;
                }

                if (slot == macro.Exit)
                {
                    exitCell = i;
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Walks the finished level from the start through open, compatible
    /// socket pairs, and checks the exit and every structure were reached.
    /// The plan's required links should guarantee this; checking the cells
    /// actually placed is what catches a content bug the plan can't see.
    /// </summary>
    private static bool Reachable(List<PlacedPiece> pieces, TemplateLibrary library, int start, int exit, MacroPlan macro, out string failure)
    {
        var at = new Dictionary<Int3, int>();
        for (int i = 0; i < pieces.Count; i++)
        {
            foreach (Int3 slot in pieces[i].Covered())
            {
                at[slot] = i;
            }
        }

        var seen = new bool[pieces.Count];
        var queue = new Queue<int>();
        seen[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            PlacedPiece piece = pieces[queue.Dequeue()];
            foreach ((Int3 local, CellFace face) in piece.Variant.OuterFaces)
            {
                Int3 slot = piece.Anchor + local;
                Int3 neighbour = slot + SlotMath.Offset(face);
                if (!at.TryGetValue(neighbour, out int other) || seen[other]
                    || piece.SocketAt(slot, face) is not { } ours
                    || pieces[other].SocketAt(neighbour, SlotMath.Opposite(face)) is not { } theirs
                    || !library.Compatible(ours, theirs))
                {
                    continue;
                }

                seen[other] = true;
                queue.Enqueue(other);
            }
        }

        failure = string.Empty;
        if (!seen[exit])
        {
            failure = "the exit cannot be reached from the start";
            return false;
        }

        foreach (StructurePlacement structure in macro.Structures)
        {
            if (!seen[at[structure.Pieces[0].Anchor]])
            {
                failure = $"structure '{structure.Def.Id}' cannot be reached from the start";
                return false;
            }
        }

        return true;
    }
}
