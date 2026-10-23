namespace Cellgen.Generation;

/// <summary>A winding corridor from start to exit with side branches; every other slot stays empty.</summary>
internal sealed class CorridorsLayout : ILayoutPlanner
{
    /// <inheritdoc/>
    public string Id => BuiltinLayouts.Corridors;

    /// <inheritdoc/>
    public MacroPlan? Plan(LayoutRequest request, out string failure) =>
        new MacroPlanner(request.Def, request.Library, request.Context, request.Rng, request.Report).Plan(out failure);
}
