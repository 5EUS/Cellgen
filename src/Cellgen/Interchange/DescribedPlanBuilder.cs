using Cellgen.Generation;

namespace Cellgen.Interchange;

/// <summary>Checks a <see cref="PlanDescription"/> against its recipe and builds the <see cref="MacroPlan"/> it describes.</summary>
internal static class DescribedPlanBuilder
{
    private const int MaxProblemsShown = 6;

    /// <summary>Builds a plan from a description.</summary>
    /// <param name="description">The description.</param>
    /// <param name="request">The recipe, context, random stream and report of the attempt.</param>
    /// <param name="failure">Everything wrong with the description, when null is returned.</param>
    /// <returns>The plan, or null when the description is invalid.</returns>
    internal static MacroPlan? Build(PlanDescription description, LayoutRequest request, out string failure)
    {
        var problems = new List<string>();
        var checker = new Checker(request.Def, problems);
        if (description.Version != PlanDescription.CurrentVersion)
        {
            failure = $"plan description version {description.Version} is not supported; this library reads version {PlanDescription.CurrentVersion}";
            return null;
        }

        Dictionary<Int3, SlotRole> slots = checker.Slots(description.Slots);
        List<Int3> path = checker.Path(description.Path, slots);
        List<(Int3 A, Int3 B)> links = checker.Links(description.Links, slots);
        List<(Int3 At, string Tag)> tags = checker.Tags(description.RequireTag, slots);
        List<(Int3 At, CellFace Face, string Socket)> sockets = checker.Sockets(description.RequireSocket, slots);
        List<(StructureData Structure, int Index, int? Count, DescribedRegion? Region)> structures = checker.Structures(description.Structures);
        if (problems.Count > 0)
        {
            failure = Summarize(problems);
            return null;
        }

        MacroPlan plan = Assemble(slots, path, links, tags, sockets);
        return PlaceStructures(plan, structures, request, out failure) ? plan : null;
    }

    private static MacroPlan Assemble(
        Dictionary<Int3, SlotRole> slots,
        List<Int3> path,
        List<(Int3 A, Int3 B)> links,
        List<(Int3 At, string Tag)> tags,
        List<(Int3 At, CellFace Face, string Socket)> sockets)
    {
        var plan = new MacroPlan();
        foreach (KeyValuePair<Int3, SlotRole> slot in slots)
        {
            plan.Occupy(slot.Key, slot.Value);
        }

        for (int i = 0; i < path.Count; i++)
        {
            plan.AppendPath(path[i]);
            if (i > 0)
            {
                plan.Link(path[i - 1], path[i]);
            }
        }

        foreach ((Int3 a, Int3 b) in links)
        {
            plan.Link(a, b);
        }

        foreach ((Int3 at, string tag) in tags)
        {
            plan.RequireTag(at, tag);
        }

        foreach ((Int3 at, CellFace face, string socket) in sockets)
        {
            plan.RequireSocket(at, face, socket);
        }

        return plan;
    }

    private static bool PlaceStructures(
        MacroPlan plan,
        List<(StructureData Structure, int Index, int? Count, DescribedRegion? Region)> structures,
        LayoutRequest request,
        out string failure)
    {
        var placedBy = new Dictionary<StructureData, int>();
        foreach ((StructureData structure, int index, int? count, DescribedRegion? region) in structures)
        {
            PlacementArea where = AreaFor(request.Def, region, plan);
            int wanted = count ?? StructurePlacer.CountFor(request.Def, structure, request.Rng, request.Context);
            int placed = placedBy.GetValueOrDefault(structure);
            for (int i = 0; i < wanted && StructurePlacer.Place(structure, $"structures[{index - 1}]", plan, where, request.Rng, request.Report); i++)
            {
                placed++;
            }

            placedBy[structure] = placed;
        }

        for (int i = 0; i < request.Def.Structures.Count; i++)
        {
            StructureData structure = request.Def.Structures[i];
            if (structure.Requirement == StructureRequirement.Required && placedBy.GetValueOrDefault(structure) < Math.Max(1, structure.MinCount))
            {
                failure = $"structures[{i}] ('{structure.Id}') is required but the layout did not place it";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    /// <summary>Where a structure may go: the described region, or the grid inside its border.</summary>
    private static PlacementArea AreaFor(LevelData def, DescribedRegion? region, MacroPlan plan)
    {
        int width = def.GridSize.X;
        int depth = def.GridSize.Y;
        int layer = region?.Layer ?? (def.AllowedLayers.Count > 0 ? def.AllowedLayers[0] : 0);
        (int MinX, int MaxX, int MinZ, int MaxZ) open = Bounds(region?.Playable, (1, width - 2, 1, depth - 2));
        (int MinX, int MaxX, int MinZ, int MaxZ) room = Bounds(region?.Usable ?? region?.Playable, open);
        IReadOnlyList<int> avoid = region?.AvoidColumns ?? [];
        return new PlacementArea(
            layer,
            width,
            depth,
            slot => Inside(slot, layer, open),
            slot => Inside(slot, layer, room) && !avoid.Contains(slot.X) && !plan.IsOccupied(slot));
    }

    private static (int MinX, int MaxX, int MinZ, int MaxZ) Bounds(DescribedBox? box, (int MinX, int MaxX, int MinZ, int MaxZ) fallback) =>
        box is null ? fallback : (box.X[0], box.X[1], box.Z[0], box.Z[1]);

    private static bool Inside(Int3 slot, int layer, (int MinX, int MaxX, int MinZ, int MaxZ) box) =>
        slot.Y == layer && slot.X >= box.MinX && slot.X <= box.MaxX && slot.Z >= box.MinZ && slot.Z <= box.MaxZ;

    private static string Summarize(List<string> problems)
    {
        string shown = string.Join("; ", problems.Take(MaxProblemsShown));
        return problems.Count > MaxProblemsShown ? $"{shown}; and {problems.Count - MaxProblemsShown} more" : shown;
    }

    /// <summary>Collects problems while reading the parts of a description.</summary>
    private sealed class Checker(LevelData def, List<string> problems)
    {
        private readonly IReadOnlyList<int> layers = def.AllowedLayers.Count > 0 ? def.AllowedLayers : [0];

        internal Dictionary<Int3, SlotRole> Slots(IReadOnlyList<DescribedSlot> described)
        {
            var slots = new Dictionary<Int3, SlotRole>();
            for (int i = 0; i < described.Count; i++)
            {
                string path = $"slots[{i}]";
                if (this.Slot(described[i].At, path) is not { } at)
                {
                    continue;
                }

                SlotRole? role = described[i].Role switch
                {
                    "start" => SlotRole.Start,
                    "path" => SlotRole.Path,
                    "branch" => SlotRole.Branch,
                    "exit" => SlotRole.Exit,
                    _ => null,
                };
                if (role is null)
                {
                    problems.Add($"{path}.role '{described[i].Role}' is not start, path, branch or exit");
                }
                else if (!slots.TryAdd(at, role.Value))
                {
                    problems.Add($"{path} {at} is listed twice");
                }
            }

            return slots;
        }

        internal List<Int3> Path(IReadOnlyList<IReadOnlyList<int>> described, Dictionary<Int3, SlotRole> slots)
        {
            var path = new List<Int3>(described.Count);
            for (int i = 0; i < described.Count; i++)
            {
                if (this.Slot(described[i], $"path[{i}]") is { } at)
                {
                    path.Add(at);
                }
            }

            if (path.Count < 2)
            {
                problems.Add("path needs at least a start and an exit");
                return path;
            }

            for (int i = 0; i < path.Count; i++)
            {
                SlotRole? role = slots.TryGetValue(path[i], out SlotRole found) ? found : null;
                SlotRole wanted = PathRole(i, path.Count);
                if (role != wanted)
                {
                    problems.Add($"path[{i}] {path[i]} must be a slot with role {RoleName(wanted)}");
                }

                if (i > 0 && !Adjacent(path[i - 1], path[i]))
                {
                    problems.Add($"path[{i}] {path[i]} is not next to path[{i - 1}] {path[i - 1]}");
                }
            }

            foreach (KeyValuePair<Int3, SlotRole> slot in slots.Where(s => s.Value is SlotRole.Start or SlotRole.Exit or SlotRole.Path))
            {
                if (!path.Contains(slot.Key))
                {
                    problems.Add($"slot {slot.Key} has role {RoleName(slot.Value)} but is not on the path");
                }
            }

            return path;
        }

        internal List<(Int3 A, Int3 B)> Links(IReadOnlyList<DescribedLink> described, Dictionary<Int3, SlotRole> slots)
        {
            var links = new List<(Int3, Int3)>(described.Count);
            for (int i = 0; i < described.Count; i++)
            {
                if (this.Slot(described[i].A, $"links[{i}].a") is not { } a || this.Slot(described[i].B, $"links[{i}].b") is not { } b)
                {
                    continue;
                }

                if (!slots.ContainsKey(a) || !slots.ContainsKey(b))
                {
                    problems.Add($"links[{i}] joins {a} and {b}, but both must be listed in slots");
                }
                else if (!Adjacent(a, b))
                {
                    problems.Add($"links[{i}]: {a} and {b} are not neighbours");
                }
                else
                {
                    links.Add((a, b));
                }
            }

            return links;
        }

        internal List<(Int3 At, string Tag)> Tags(IReadOnlyList<DescribedTag> described, Dictionary<Int3, SlotRole> slots)
        {
            var tags = new List<(Int3, string)>(described.Count);
            for (int i = 0; i < described.Count; i++)
            {
                if (this.Occupied(described[i].At, $"require_tag[{i}]", slots) is not { } at)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(described[i].Tag))
                {
                    problems.Add($"require_tag[{i}].tag is empty");
                }
                else
                {
                    tags.Add((at, described[i].Tag));
                }
            }

            return tags;
        }

        internal List<(Int3 At, CellFace Face, string Socket)> Sockets(IReadOnlyList<DescribedSocket> described, Dictionary<Int3, SlotRole> slots)
        {
            var sockets = new List<(Int3, CellFace, string)>(described.Count);
            for (int i = 0; i < described.Count; i++)
            {
                if (this.Occupied(described[i].At, $"require_socket[{i}]", slots) is not { } at)
                {
                    continue;
                }

                CellFace? face = described[i].Face switch
                {
                    "north" => CellFace.North,
                    "east" => CellFace.East,
                    "south" => CellFace.South,
                    "west" => CellFace.West,
                    "up" => CellFace.Up,
                    "down" => CellFace.Down,
                    _ => null,
                };
                if (face is null)
                {
                    problems.Add($"require_socket[{i}].face '{described[i].Face}' is not north, east, south, west, up or down");
                }
                else if (string.IsNullOrEmpty(described[i].Socket))
                {
                    problems.Add($"require_socket[{i}].socket is empty");
                }
                else
                {
                    sockets.Add((at, face.Value, described[i].Socket));
                }
            }

            return sockets;
        }

        internal List<(StructureData Structure, int Index, int? Count, DescribedRegion? Region)> Structures(IReadOnlyList<DescribedStructure> described)
        {
            var structures = new List<(StructureData, int, int?, DescribedRegion?)>(described.Count);
            for (int i = 0; i < described.Count; i++)
            {
                DescribedStructure one = described[i];
                string path = $"structures[{i}]";
                if (one.Index < 1 || one.Index > def.Structures.Count)
                {
                    problems.Add($"{path}.index {one.Index} is not one of the recipe's {def.Structures.Count} structures (counting from 1)");
                    continue;
                }

                if (one.Count < 0)
                {
                    problems.Add($"{path}.count is negative");
                    continue;
                }

                if (this.RegionIsValid(one.Region, path))
                {
                    structures.Add((def.Structures[one.Index - 1], one.Index, one.Count, one.Region));
                }
            }

            return structures;
        }

        private static SlotRole PathRole(int position, int length)
        {
            if (position == 0)
            {
                return SlotRole.Start;
            }

            return (position == length - 1) ? SlotRole.Exit : SlotRole.Path;
        }

        private static string RoleName(SlotRole role) => role switch
        {
            SlotRole.Start => "start",
            SlotRole.Exit => "exit",
            SlotRole.Branch => "branch",
            _ => "path",
        };

        private static bool Adjacent(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z) == 1;

        private Int3? Slot(IReadOnlyList<int> at, string path)
        {
            if (at.Count != 3)
            {
                problems.Add($"{path} must be [x, y, z], has {at.Count} numbers");
                return null;
            }

            var slot = new Int3(at[0], at[1], at[2]);
            if (slot.X < 0 || slot.X >= def.GridSize.X || slot.Z < 0 || slot.Z >= def.GridSize.Y || !this.layers.Contains(slot.Y))
            {
                problems.Add($"{path} {slot} is outside the {def.GridSize.X}x{def.GridSize.Y} grid or its layers");
                return null;
            }

            return slot;
        }

        private Int3? Occupied(IReadOnlyList<int> at, string path, Dictionary<Int3, SlotRole> slots)
        {
            if (this.Slot(at, $"{path}.at") is not { } slot)
            {
                return null;
            }

            if (!slots.ContainsKey(slot))
            {
                problems.Add($"{path}.at {slot} is not listed in slots");
                return null;
            }

            return slot;
        }

        private bool RegionIsValid(DescribedRegion? region, string path)
        {
            if (region is null)
            {
                return true;
            }

            bool valid = true;
            if (region.Layer is { } layer && !this.layers.Contains(layer))
            {
                problems.Add($"{path}.region.layer {layer} is not an allowed layer");
                valid = false;
            }

            bool playable = this.BoxIsValid(region.Playable, $"{path}.region.playable");
            bool usable = this.BoxIsValid(region.Usable, $"{path}.region.usable");
            return valid && playable && usable;
        }

        private bool BoxIsValid(DescribedBox? box, string path)
        {
            if (box is null)
            {
                return true;
            }

            if (box.X.Count != 2 || box.Z.Count != 2 || box.X[0] > box.X[1] || box.Z[0] > box.Z[1])
            {
                problems.Add($"{path} needs x and z as [min, max]");
                return false;
            }

            return true;
        }
    }
}
