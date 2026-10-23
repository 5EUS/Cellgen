namespace Cellgen.Generation;

/// <summary>The layout planners a generation can choose from, by id.</summary>
public sealed class LayoutRegistry
{
    private readonly Dictionary<string, ILayoutPlanner> planners = new(StringComparer.Ordinal);
    private bool frozen;

    /// <summary>Gets a shared registry of the built-in layouts, which cannot be added to: copy it with <see cref="CreateBuiltin"/>.</summary>
    public static LayoutRegistry Builtin { get; } = CreateFrozenBuiltin();

    /// <summary>Gets the ids of every registered planner, sorted.</summary>
    public IReadOnlyList<string> Ids => [.. this.planners.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Creates a registry holding the built-in layouts, ready to add host or mod layouts to.</summary>
    /// <returns>A new, mutable registry.</returns>
    public static LayoutRegistry CreateBuiltin()
    {
        var registry = new LayoutRegistry();
        registry.Register(new CorridorsLayout());
        registry.Register(new WildsLayout());
        return registry;
    }

    /// <summary>Adds a planner.</summary>
    /// <param name="planner">The planner.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, or the id is taken.</exception>
    public void Register(ILayoutPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(planner);
        if (this.frozen)
        {
            throw new InvalidOperationException("This registry is shared and read-only; use LayoutRegistry.CreateBuiltin() for one you can add to.");
        }

        if (!this.planners.TryAdd(planner.Id, planner))
        {
            throw new InvalidOperationException($"A layout planner with id '{planner.Id}' is already registered.");
        }
    }

    /// <summary>Removes a planner, such as one that came from a script that has been reloaded.</summary>
    /// <param name="id">The planner's id.</param>
    /// <returns>True when there was one.</returns>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    public bool Unregister(string id)
    {
        if (this.frozen)
        {
            throw new InvalidOperationException("This registry is shared and read-only; use LayoutRegistry.CreateBuiltin() for one you can change.");
        }

        return this.planners.Remove(id);
    }

    /// <summary>Finds a planner.</summary>
    /// <param name="id">The id a recipe names.</param>
    /// <returns>The planner, or null when none has that id.</returns>
    public ILayoutPlanner? Find(string id) => this.planners.GetValueOrDefault(id);

    private static LayoutRegistry CreateFrozenBuiltin()
    {
        LayoutRegistry registry = CreateBuiltin();
        registry.frozen = true;
        return registry;
    }
}
