namespace Cellgen.Generation;

/// <summary>Everything a level depends on besides its recipe.</summary>
public sealed class GenContext
{
    private static readonly Dictionary<string, object> NoFacts = new(StringComparer.Ordinal);

    /// <summary>Gets the level's seed.</summary>
    public ulong Seed { get; init; }

    /// <summary>Gets the run depth, from 0.</summary>
    public int Depth { get; init; }

    /// <summary>Gets the meta-progression flags set so far, which unlock structures.</summary>
    public IReadOnlySet<string> UnlockFlags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the encounters zones can be given.</summary>
    public EncounterCatalog Encounters { get; init; } = EncounterCatalog.Empty;

    /// <summary>Gets the layout planners recipes can name. Defaults to the built-in layouts; add a host's own with <see cref="LayoutRegistry.CreateBuiltin"/>.</summary>
    public LayoutRegistry Layouts { get; init; } = LayoutRegistry.Builtin;

    /// <summary>Gets the policies layered over generation, in order. Empty by default: see <see cref="IGenerationPolicy"/>.</summary>
    public IReadOnlyList<IGenerationPolicy> Policies { get; init; } = [];

    /// <summary>Gets the sources that make a policy for each generation; see <see cref="IGenerationPolicySource"/>.</summary>
    public IReadOnlyList<IGenerationPolicySource> PolicySources { get; init; } = [];

    /// <summary>Gets what the host knows about the run that a recipe cannot: preset, campaign, anything. Policies and scripts may read it; Cellgen never does.</summary>
    public IReadOnlyDictionary<string, object> Facts { get; init; } = NoFacts;

    /// <summary>Gets where loot tables find the entries of the pools they draw from.</summary>
    public ILootCatalog Loot { get; init; } = ILootCatalog.Empty;

    /// <summary>Gets a value indicating whether any policy is installed. Hot paths skip building queries when none is.</summary>
    internal bool HasPolicies => this.Policies.Count > 0;

    /// <summary>Gets a copy of this context for another seed and depth, keeping every catalog, registry and unlock.</summary>
    /// <param name="seed">The new seed.</param>
    /// <param name="depth">The new run depth.</param>
    /// <returns>The copy.</returns>
    public GenContext With(ulong seed, int depth) => new()
    {
        Seed = seed,
        Depth = depth,
        UnlockFlags = this.UnlockFlags,
        Encounters = this.Encounters,
        Layouts = this.Layouts,
        Loot = this.Loot,
        Policies = this.Policies,
        PolicySources = this.PolicySources,
        Facts = this.Facts,
    };

    /// <summary>Gets a copy of this context with a different set of policies, as the generator runs a recipe under the ones its sources made.</summary>
    /// <param name="policies">The policies.</param>
    /// <returns>The copy.</returns>
    internal GenContext WithPolicies(IReadOnlyList<IGenerationPolicy> policies) => new()
    {
        Seed = this.Seed,
        Depth = this.Depth,
        UnlockFlags = this.UnlockFlags,
        Encounters = this.Encounters,
        Layouts = this.Layouts,
        Loot = this.Loot,
        Policies = policies,
        PolicySources = this.PolicySources,
        Facts = this.Facts,
    };
}
