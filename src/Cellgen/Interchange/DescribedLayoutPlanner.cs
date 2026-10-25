using Cellgen.Generation;

namespace Cellgen.Interchange;

/// <summary>
/// A layout planner whose plan comes from outside C#, as a <see cref="PlanDescription"/> in JSON:
/// a script, a data file, a service. The host supplies the function that produces the JSON for
/// an attempt; Cellgen checks it against the recipe (bounds, adjacency, roles, structures) and
/// builds the plan, so a bad description fails the attempt with a reason instead of corrupting it.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="DescribedLayoutPlanner"/> class.</remarks>
/// <param name="id">The layout id recipes name.</param>
/// <param name="describe">Produces the plan description JSON for an attempt, or null when it could not (the attempt then fails).</param>
public sealed class DescribedLayoutPlanner(string id, Func<LayoutRequest, string?> describe) : ILayoutPlanner
{
    private readonly Func<LayoutRequest, string?> describe = describe;

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public MacroPlan? Plan(LayoutRequest request, out string failure)
    {
        string? json = this.describe(request);
        if (string.IsNullOrWhiteSpace(json))
        {
            failure = $"layout '{this.Id}' produced no plan";
            return null;
        }

        PlanDescription? description = PlanDescriptionReader.Parse(json, out string error);
        if (description is null)
        {
            failure = $"layout '{this.Id}': {error}";
            return null;
        }

        MacroPlan? plan = DescribedPlanBuilder.Build(description, request, out string invalid);
        failure = (plan is null) ? $"layout '{this.Id}': {invalid}" : string.Empty;
        return plan;
    }
}
