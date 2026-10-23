namespace Cellgen.Generation;

/// <summary>What a layout planner is given for one attempt.</summary>
public sealed class LayoutRequest
{
    /// <summary>Initializes a new instance of the <see cref="LayoutRequest"/> class.</summary>
    /// <param name="def">The recipe.</param>
    /// <param name="context">Seed, depth, unlocks and catalogs.</param>
    /// <param name="library">What can be placed.</param>
    /// <param name="rng">This stage's random stream.</param>
    /// <param name="report">Collects diagnostics.</param>
    internal LayoutRequest(LevelData def, GenContext context, TemplateLibrary library, GenRng rng, GenReport report)
    {
        this.Def = def;
        this.Context = context;
        this.Library = library;
        this.Rng = rng;
        this.Report = report;
    }

    /// <summary>Gets the recipe, including <see cref="LevelData.LayoutSettings"/> for this layout.</summary>
    public LevelData Def { get; }

    /// <summary>Gets the seed, depth, unlock flags and catalogs.</summary>
    public GenContext Context { get; }

    /// <summary>Gets the random stream for this stage. The only source of randomness a planner may use.</summary>
    public GenRng Rng { get; }

    /// <summary>Gets the report diagnostics go to.</summary>
    public GenReport Report { get; }

    /// <summary>Gets the cells and turns available to the recipe.</summary>
    internal TemplateLibrary Library { get; }
}
