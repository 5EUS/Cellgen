namespace Cellgen.Generation;

/// <summary>A whole-grid wilderness: a border ring, far-apart start and exit, a few tall landmarks, and the solver filling the rest.</summary>
internal sealed class WildsLayout : ILayoutPlanner
{
    /// <inheritdoc/>
    public string Id => BuiltinLayouts.Wilds;

    /// <inheritdoc/>
    public MacroPlan? Plan(LayoutRequest request, out string failure) =>
        new WildsPlanner(request.Def, request.Context, request.Rng, request.Report).Plan(out failure);
}
